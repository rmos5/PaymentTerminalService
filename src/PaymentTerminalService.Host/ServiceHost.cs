using System;
using System.Configuration;
using System.ServiceProcess;

namespace PaymentTerminalService.Host
{
    internal sealed class ServiceHost : ServiceBase
    {
        private WebHostRunner webHostRunner;

        public ServiceHost()
        {
            ServiceName = Program.AppName;
            CanStop = true;
            CanShutdown = true;   // important
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                webHostRunner = new WebHostRunner(ConfigurationManager.AppSettings["BaseUrl"]);
                webHostRunner.Start();
            }
            catch (Exception)
            {
                Program.ShutdownLoggingOnce();
                throw;
            }
        }

        protected override void OnStop()
        {
            StopInternal();
        }

        protected override void OnShutdown()
        {
            StopInternal();
        }

        private void StopInternal()
        {
            try
            {
                // Stop background work FIRST
                if (webHostRunner != null)
                {
                    try { webHostRunner.Stop(); } catch { }
                    webHostRunner = null;
                }
            }
            finally
            {
                // Then flush/shutdown logging ONCE
                Program.ShutdownLoggingOnce();
            }
        }
    }
}
