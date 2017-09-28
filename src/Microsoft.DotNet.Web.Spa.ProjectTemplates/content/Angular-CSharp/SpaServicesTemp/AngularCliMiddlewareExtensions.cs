using AngularSpa.SpaServicesTemp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using System;

namespace Microsoft.AspNetCore.Builder
{
    public static class AngularCliMiddlewareExtensions
    {
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
            this SpaBuilder spaBuilder,
            string sourcePath,
            Action<AngularCliMiddlewareOptions> configureOptions = null)
        {
            new AngularCliMiddleware(sourcePath, configureOptions, spaBuilder);
        }
    }
}
