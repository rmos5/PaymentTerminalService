using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Threading;

namespace PaymentTerminalService.Host
{
    internal static class Program
    {
        public static readonly string AppName = "PaymentTerminalService";
        private static readonly string Version = "1.0.0";
        private static string LogDir { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        private static Log Log { get; set; }

        // Ensures logging shutdown happens exactly once
        private static int logShutdown;

        static int Main(string[] args)
        {
            string configLogDir = ConfigurationManager.AppSettings["LogDirectory"]?.Trim();
            if (!string.IsNullOrEmpty(configLogDir))
                LogDir = configLogDir;

            ProcessArgs(args);

            Log = new Log(AppName, Version, LogDir);

            // Safety net only
            AppDomain.CurrentDomain.ProcessExit += (s, e) => ShutdownLoggingOnce();

            Trace.WriteLine($"{AppName} starting...", typeof(Program).FullName);

            try
            {
                if (Environment.UserInteractive)
                {
                    RunAsConsole();
                    return 0;
                }
                else
                {
                    RunAsService();
                    return 0; // SCM ignores exit code
                }
            }
            catch (Exception ex)
            {
                // Loud for humans
                try
                {
                    Trace.WriteLine(ex.ToString(), typeof(Program).FullName);

                    if (Environment.UserInteractive)
                    {
                        Console.WriteLine($"{ex}", typeof(Program).FullName);
                    }
                }
                catch
                {
                    // ignore
                }

                return 1;
            }
            finally
            {
                Trace.WriteLine($"{AppName} ended.", typeof(Program).FullName);
                ShutdownLoggingOnce();
            }
        }
       
        internal static void ShutdownLoggingOnce()
        {
            if (Interlocked.Exchange(ref logShutdown, 1) != 0)
                return;

            try { Log?.Shutdown(); } catch { }
        }


        private static void ProcessArgs(string[] args)
        {
            if (args == null || args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
                return;

            string candidate = args[0].Trim();

            foreach (char c in Path.GetInvalidPathChars())
            {
                if (candidate.IndexOf(c) >= 0)
                {
                    Trace.WriteLine($"Invalid logDir argument: '{candidate}'", typeof(Program).FullName);
                    return;
                }
            }

            try
            {
                if (!Directory.Exists(candidate))
                    Directory.CreateDirectory(candidate);

                LogDir = candidate;
            }
            catch
            {
                Trace.WriteLine($"Failed to use logDir argument: '{candidate}'", typeof(Program).FullName);
            }
        }

        private static void RunAsConsole()
        {
            using (var web = new WebHostRunner(ConfigurationManager.AppSettings["BaseUrl"]))
            {
                web.Start();

                Console.WriteLine($"{AppName} running (console mode).");
                Console.WriteLine("Press ENTER to stop.");
                Console.ReadLine();
            }
        }

        private static void RunAsService()
        {
            ServiceBase.Run(new ServiceHost());
        }
    }
}
