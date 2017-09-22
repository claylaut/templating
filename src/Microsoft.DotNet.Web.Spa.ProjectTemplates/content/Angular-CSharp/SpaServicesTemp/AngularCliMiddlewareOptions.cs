using System.Collections.Generic;

namespace Microsoft.AspNetCore.SpaServices.AngularCli
{
    /// <summary>
    /// Options for configuring an Angular CLI middleware instance.
    /// </summary>
    public class AngularCliMiddlewareOptions
    {   
        /// <summary>
        /// Specifies additional environment variables to be passed to the Node instance hosting
        /// the webpack compiler.
        /// </summary>
        public IDictionary<string, string> EnvironmentVariables { get; set; }
    }
}