using System;
using System.IO;
using Microsoft.AspNetCore.NodeServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Microsoft.AspNetCore.SpaServices.AngularCli;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Extension methods that can be used to enable Angular CLI middleware support.
    /// </summary>
    public static class AngularCliMiddleware
    {
        private static readonly JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            TypeNameHandling = TypeNameHandling.None
        };

        /// <summary>
        /// Enables Angular CLI middleware support. This hosts an instance of the Angular CLI in memory in
        /// your application so that you can always serve up-to-date CLI-built resources without having
        /// to run CLI server manually.
        ///
        /// Incoming requests that match Angular CLI-built files will be handled by returning the CLI server
        /// output directly.
        ///
        /// This feature should only be used in development. For production deployments, be sure not to
        /// enable Angular CLI middleware.
        /// </summary>
        /// <param name="appBuilder">The <see cref="IApplicationBuilder"/>.</param>
        /// <param name="options">Options for configuring the Angular CLI instance.</param>
        public static void UseAngularCliMiddleware(
            this IApplicationBuilder appBuilder,
            string angularAppRoot,
            Action<AngularCliMiddlewareOptions> configureOptions = null)
        {
            if (angularAppRoot == null)
            {
                throw new ArgumentNullException(nameof(angularAppRoot));
            }

            // Prepare options
            var options = new AngularCliMiddlewareOptions();
            configureOptions?.Invoke(options);

            // Unlike other consumers of NodeServices, AngularCliMiddleware dosen't share Node instances, nor does it
            // use your DI configuration. It's important for AngularCliMiddleware to have its own private Node instance
            // because it must *not* restart when files change (it's designed to watch for changes and rebuild).
            var nodeServicesOptions = new NodeServicesOptions(appBuilder.ApplicationServices);
            nodeServicesOptions.WatchFileExtensions = new string[] { }; // Don't watch anything
            nodeServicesOptions.ProjectPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                angularAppRoot);

            if (options.EnvironmentVariables != null)
            {
                foreach (var kvp in options.EnvironmentVariables)
                {
                    nodeServicesOptions.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            var nodeServices = NodeServicesFactory.CreateNodeServices(nodeServicesOptions);

            // Get a filename matching the middleware Node script
            var script = EmbeddedResourceReader.Read(typeof(AngularCliMiddleware),
                "/SpaServicesTemp/angular-cli-middleware.js");
            var nodeScript = new StringAsTempFile(script, nodeServicesOptions.ApplicationStoppingToken); // Will be cleaned up on process exit
            var nodeScriptFilename = nodeScript.FileName;

            // Tell Node to start the server hosting the Angular CLI
            var angularCliOptions = new {};
            var angularCliServerInfo =
                nodeServices.InvokeExportAsync<AngularCliServerInfo>(nodeScript.FileName, "startAngularCliServer",
                    JsonConvert.SerializeObject(angularCliOptions, jsonSerializerSettings)).Result;

            // Proxy the corresponding requests through ASP.NET and into the Node listener
            // Anything under /<publicpath> (e.g., /dist) is proxied as a normal HTTP request with a typical timeout (100s is the default from HttpClient),
            // plus the HMR endpoint is proxied with infinite timeout, because it's an EventSource (long-lived request).
            foreach (var publicPath in angularCliServerInfo.PublicPaths)
            {
                // TODO: Proxy the HMR endpoint
                // appBuilder.UseProxyToLocalAngularCliMiddleware(publicPath + hmrEndpoint, angularCliServerInfo.Port, Timeout.InfiniteTimeSpan);

                appBuilder.UseProxyToLocalAngularCliMiddleware(publicPath, angularCliServerInfo.Port, TimeSpan.FromSeconds(100));
            }
        }

        private static void UseProxyToLocalAngularCliMiddleware(this IApplicationBuilder appBuilder, string publicPath, int proxyToPort, TimeSpan requestTimeout)
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
            var proxyOptions = new ConditionalProxyMiddlewareOptions(
                "http", "localhost", proxyToPort.ToString(), requestTimeout);
            appBuilder.UseMiddleware<ConditionalProxyMiddleware>(publicPath, proxyOptions);
        }

#pragma warning disable CS0649
        class AngularCliServerInfo
        {
            public int Port { get; set; }
            public string[] PublicPaths { get; set; }
        }
    }
#pragma warning restore CS0649
}