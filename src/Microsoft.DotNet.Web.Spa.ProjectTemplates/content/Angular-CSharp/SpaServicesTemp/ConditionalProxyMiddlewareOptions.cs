using System;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.SpaServices.AngularCli
{
    internal class ConditionalProxyMiddlewareOptions
    {
        public ConditionalProxyMiddlewareOptions(string scheme, string host, Task<string> port, TimeSpan requestTimeout)
        {
            Scheme = scheme;
            Host = host;
            Port = port;
            RequestTimeout = requestTimeout;
        }

        public string Scheme { get; }
        public string Host { get; }
        public Task<string> Port { get; }
        public TimeSpan RequestTimeout { get; }
    }
}