using System;
using System.IO;
using Microsoft.AspNetCore.NodeServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;

namespace AngularSpa.SpaServicesTemp
{
    /// <summary>
    /// Extension methods that can be used to enable Angular CLI middleware support.
    /// </summary>
    internal class AngularCliMiddleware
    {
        private INodeServices _nodeServices;
        private string _middlewareScriptPath;

        private static readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            TypeNameHandling = TypeNameHandling.None
        };

        public AngularCliMiddleware(
            string sourcePath,
            Action<AngularCliMiddlewareOptions> configureOptions,
            SpaBuilder spaBuilder)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException("Cannot be null or empty", nameof(sourcePath));
            }

            // Prepare options
            var options = new AngularCliMiddlewareOptions();
            configureOptions?.Invoke(options);

            // Start middleware service and attach to middleware pipeline
            var appBuilder = spaBuilder.AppBuilder;
            PrepareNodeServicesInstance(appBuilder, sourcePath, options);
            var angularCliServerInfoTask = StartAngularCliServerAsync();
            spaBuilder.AddStartupTask(angularCliServerInfoTask);

            // Proxy the corresponding requests through ASP.NET and into the Node listener
            // Anything under /<publicpath> (e.g., /dist) is proxied as a normal HTTP request with a typical timeout (100s is the default from HttpClient),
            UseProxyToLocalAngularCliMiddleware(appBuilder, spaBuilder.PublicPath, angularCliServerInfoTask, TimeSpan.FromSeconds(100));

            // TODO: Proxy the HMR endpoint with infinite timeout, because it's an EventSource (long-lived request).
            // appBuilder.UseProxyToLocalAngularCliMiddleware(publicPath + hmrEndpoint, angularCliServerInfo.Port, Timeout.InfiniteTimeSpan);

            // Advertise the availability of this feature to other SPA middleware
            spaBuilder.Properties.Add(this, null);
        }

        private void PrepareNodeServicesInstance(IApplicationBuilder appBuilder, string sourcePath, AngularCliMiddlewareOptions options)
        {
            // Unlike other consumers of NodeServices, AngularCliMiddleware dosen't share Node instances, nor does it
            // use your DI configuration. It's important for AngularCliMiddleware to have its own private Node instance
            // because it must *not* restart when files change (it's designed to watch for changes and rebuild).
            var nodeServicesOptions = new NodeServicesOptions(appBuilder.ApplicationServices);
            nodeServicesOptions.WatchFileExtensions = new string[] { }; // Don't watch anything
            nodeServicesOptions.ProjectPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                sourcePath);

            if (options.EnvironmentVariables != null)
            {
                foreach (var kvp in options.EnvironmentVariables)
                {
                    nodeServicesOptions.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            _nodeServices = NodeServicesFactory.CreateNodeServices(nodeServicesOptions);

            // Get a filename matching the middleware Node script
            var script = EmbeddedResourceReader.Read(typeof(AngularCliMiddleware),
                "/SpaServicesTemp/angular-cli-middleware.js");
            var nodeScript = new StringAsTempFile(script, nodeServicesOptions.ApplicationStoppingToken); // Will be cleaned up on process exit
            _middlewareScriptPath = nodeScript.FileName;
        }

        private async Task<AngularCliServerInfo> StartAngularCliServerAsync()
        {
            // Tell Node to start the server hosting the Angular CLI
            var angularCliOptions = new { };
            var angularCliServerInfo =
                await _nodeServices.InvokeExportAsync<AngularCliServerInfo>(_middlewareScriptPath, "startAngularCliServer",
                    JsonConvert.SerializeObject(angularCliOptions, jsonSerializerSettings));

            // Even after the Angular CLI claims to be listening for requests, there's a short
            // period where it will give an error if you make a request too quickly. Give it
            // a moment to finish starting up.
            await Task.Delay(500);

            return angularCliServerInfo;
        }

        public Task StartAngularCliBuilderAsync(string cliAppName)
        {
            return _nodeServices.InvokeExportAsync<AngularCliServerInfo>(
                _middlewareScriptPath,
                "startAngularCliBuilder",
                /* options */ new { appName = cliAppName });
        }

        private static string RemoveTrailingSlash(string str)
        {
            return str.EndsWith('/')
                ? str.Substring(0, str.Length - 1)
                : str;
        }

        private static void UseProxyToLocalAngularCliMiddleware(IApplicationBuilder appBuilder, string publicPath, Task<AngularCliServerInfo> serverInfo, TimeSpan requestTimeout)
        {
            // Note that this is hardcoded to make requests to "localhost" regardless of the hostname of the
            // server as far as the client is concerned. This is because ConditionalProxyMiddlewareOptions is
            // the one making the internal HTTP requests, and it's going to be to some port on this machine
            // because angular-cli-middleware hosts the dev server there. We can't use the hostname that the client
            // sees, because that could be anything (e.g., some upstream load balancer) and we might not be
            // able to make outbound requests to it from here.
            // Also note that the CLI server always uses HTTP, even if your app server uses HTTPS, because
            // the CLI server has no need for HTTPS (the client doesn't see it directly - all traffic
            // to it is proxied), and the CLI service couldn't use HTTPS anyway (in general it wouldn't have
            // the necessary certificate).
            var determinePortTask = serverInfo.ContinueWith(infoTask => infoTask.Result.Port.ToString());
            var proxyOptions = new ConditionalProxyMiddlewareOptions(
                "http", "localhost", determinePortTask, requestTimeout);
            appBuilder.UseMiddleware<ConditionalProxyMiddleware>(publicPath, proxyOptions);
        }

#pragma warning disable CS0649
        class AngularCliServerInfo
        {
            public int Port { get; set; }
        }
    }
#pragma warning restore CS0649
}