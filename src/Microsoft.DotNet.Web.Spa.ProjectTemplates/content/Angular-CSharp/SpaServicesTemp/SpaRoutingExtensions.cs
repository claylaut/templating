using AngularSpa.SpaServicesTemp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.AspNetCore.SpaServices.Prerendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Builder
{
    public static class SpaRoutingExtensions
    {
        internal readonly static object IsSpaFallbackRequestTag = new object();

        public static void UseSpaFallback(
            this IApplicationBuilder app,
            string publicPath,
            Action<SpaBuilder> setup = null,
            string defaultPage = "index.html")
        {
            // Any requests other than those already routed into /<publicPath> are
            // redirected to the default file. This will then be served by:
            // 1. Server-side prerendering, if enabled
            // 2. SPA middleware (e.g., AngularCliMiddleware), if registered
            // 3. StaticFileMiddleware, if the file exists on disk
            var publicPathString = new PathString(publicPath);
            var defaultFileString = publicPathString.Add(new PathString("/" + defaultPage));
            app.Use(async (context, next) =>
            {
                if (!context.Request.Path.StartsWithSegments(publicPathString))
                {
                    context.Request.Path = defaultFileString;
                    context.Items[IsSpaFallbackRequestTag] = true;
                }
                await next.Invoke();
            });

            // Prerendering and/or SPA middleware could be configured in this callback
            setup?.Invoke(new SpaBuilder(app, publicPath));
            
            // If the default file wasn't served by any other middleware,
            // serve it as a static file from disk
            app.Map(defaultFileString, _ => app.UseStaticFiles());
        }
    }

    public class SpaBuilder
    {
        private readonly IApplicationBuilder _appBuilder;
        private readonly string _publicPath;
        private Task _startupTask = Task.CompletedTask;
        private object _startupTaskLock = new object();

        public Dictionary<object, object> Properties { get; }
            = new Dictionary<object, object>();

        public SpaBuilder(IApplicationBuilder appBuilder, string publicPath)
        {
            if (string.IsNullOrEmpty(publicPath))
            {
                throw new ArgumentException("Cannot be null or empty", nameof(publicPath));
            }

            _appBuilder = appBuilder;
            _publicPath = publicPath;
            DefaultFile = publicPath + "/index.html";
        }

        public string DefaultFile { get; set; }
        public string PublicPath => _publicPath;

        public IApplicationBuilder AppBuilder => _appBuilder;

        public void AddStartupTask(Task task)
        {
            lock (_startupTaskLock)
            {
                _startupTask = Task.WhenAll(_startupTask, task);
            }
        }

        public void UsePrerendering(string entryPoint, IPrerenderBuild buildOnDemand = null)
        {
            if (string.IsNullOrEmpty(entryPoint))
            {
                throw new ArgumentException("Cannot be null or empty", nameof(entryPoint));
            }

            // Don't start any on-demand build until a request comes in. We need to wait for all
            // middleware to be configured.
            var lazyBuildOnDemandTask = new Lazy<Task>(() => buildOnDemand?.Build(this));

            var prerenderer = (ISpaPrerenderer)_appBuilder.ApplicationServices.GetService(typeof(ISpaPrerenderer));
            _appBuilder.Use(async (context, next) =>
            {
                if (!context.Items.ContainsKey(SpaRoutingExtensions.IsSpaFallbackRequestTag))
                {
                    // Don't interfere with requests that aren't meant to render the SPA app
                    await next();
                }
                else
                {
                    var buildOnDemandTask = lazyBuildOnDemandTask.Value;
                    if (buildOnDemandTask != null && !buildOnDemandTask.IsCompleted)
                    {
                        await buildOnDemandTask;
                    }

                    // If some other SPA feature has to complete before prerendering, wait
                    // for that. For example, middleware might need to compile the entrypoint.
                    if (!_startupTask.IsCompleted)
                    {
                        await _startupTask;
                    }

                    // TODO: Add an optional "supplyCustomData" callback param so people using
                    //       UsePrerendering() can, for example, pass through cookies into the .ts code
                    // TODO: Handle 'globals' in the result? It doesn't really make sense to involve
                    //       .NET in this if the JS code is returning a complete HTML page (the JS could
                    //       emit its own <script> for globals in whatever way it wants). So it might
                    //       be enough just to deprecate that API on the .ts side.

                    // As a workaround for @angular/cli not emitting the index.html in 'server'
                    // builds, pass through a URL that can be used for obtaining it. Longer term,
                    // remove this.
                    var req = context.Request;
                    var defaultFileAbsoluteUrl = UriHelper.BuildAbsolute(
                        req.Scheme, req.Host, req.PathBase, DefaultFile);
                    var customData = new { templateUrl = defaultFileAbsoluteUrl };

                    var renderResult = await prerenderer.RenderToString(entryPoint,
                        customDataParameter: customData);

                    if (!string.IsNullOrEmpty(renderResult.RedirectUrl))
                    {
                        context.Response.Redirect(renderResult.RedirectUrl);
                    }
                    else
                    {
                        context.Response.ContentType = "text/html";
                        await context.Response.WriteAsync(renderResult.Html);
                    }
                }
            });
        }
    }

    public interface IPrerenderBuild
    {
        Task Build(SpaBuilder spaBuilder);
    }

    public class AngularCliBuild : IPrerenderBuild
    {
        private readonly string _cliAppName;

        public AngularCliBuild(string cliAppName)
        {
            _cliAppName = cliAppName;
        }

        public Task Build(SpaBuilder spaBuilder)
        {
            var angularCliMiddleware = spaBuilder.Properties.Keys.OfType<AngularCliMiddleware>().FirstOrDefault();
            if (angularCliMiddleware == null)
            {
                throw new Exception($"Cannot use {nameof (AngularCliBuild)} unless you are also using {nameof(AngularCliMiddleware)}.");
            }

            return angularCliMiddleware.StartAngularCliBuilderAsync(_cliAppName);
        }
    }
}
