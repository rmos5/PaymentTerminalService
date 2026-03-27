using Microsoft.Owin.Hosting;
using System;
using System.Diagnostics;

namespace PaymentTerminalService.Host
{
    internal sealed class WebHostRunner : IDisposable
    {
        private readonly object sync = new object();
        private IDisposable owin;
        private readonly string baseUrl;
        private bool disposed;

        public WebHostRunner(string baseUrl)
        {
            this.baseUrl = (baseUrl ?? string.Empty).Trim();
            if (!Uri.TryCreate(this.baseUrl, UriKind.Absolute, out Uri uri))
            {
                throw new ArgumentException($"Base URL is missing or invalid: '{this.baseUrl}'", nameof(baseUrl));
            }
        }

        public void Start()
        {
            lock (sync)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(WebHostRunner));
                }

                try
                {
                    if (owin != null)
                    {
                        throw new InvalidOperationException("Web host is already running.");
                    }

                    owin = WebApp.Start<Startup>(baseUrl);
                    Trace.WriteLine($"Web host listening on {baseUrl}", GetType().FullName);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"{ex}", GetType().FullName);
                    throw;
                }
            }
        }

        public void Stop()
        {
            lock (sync)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(WebHostRunner));
                }

                owin?.Dispose();
                owin = null;
                Trace.WriteLine($"Web host stopped ({baseUrl})", GetType().FullName);
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                owin?.Dispose();
                owin = null;
                disposed = true;
            }
        }
    }
}
