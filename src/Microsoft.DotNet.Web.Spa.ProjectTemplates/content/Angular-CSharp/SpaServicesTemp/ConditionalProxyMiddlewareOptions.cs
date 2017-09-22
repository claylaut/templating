using System;

namespace Microsoft.AspNetCore.SpaServices.AngularCli
{
    internal class ConditionalProxyMiddlewareOptions
    {
        public ConditionalProxyMiddlewareOptions(string scheme, string host, string port, TimeSpan requestTimeout)
        {
            Scheme = scheme;
            Host = host;
            Port = port;
            RequestTimeout = requestTimeout;
        }

        public string Scheme { get; }
        public string Host { get; }
        public string Port { get; }
        public TimeSpan RequestTimeout { get; }
    }
}