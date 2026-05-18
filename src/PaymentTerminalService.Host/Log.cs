using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using System;
using System.Diagnostics;
using System.Text;

namespace PaymentTerminalService
{
    /// <summary>
    /// Centralized NLog bootstrap for PaymentTerminalService.
    /// - Unified layout for all builds.
    /// - Logs to async buffered file target (daily + size-based archiving).
    /// - Bridges System.Diagnostics.Trace / Debug to NLog (requires NLog.Targets.Trace).
    /// </summary>
    internal class Log
    {
        public const string FileTargetName = "fileTarget";

        private readonly LoggingConfiguration _config;

        public string AppName { get; }
        public string Version { get; }
        public string LogDir { get; }

        internal Log(string appName, string version, string logDir)
        {
            AppName = appName ?? "PaymentTerminalService";
            Version = version ?? "";
            LogDir = logDir?.TrimEnd('/', '\\');

            _config = new LoggingConfiguration();

            // Variables used by layout/targets
            _config.Variables["appName"] = AppName;
            _config.Variables["version"] = Version;
            _config.Variables["logDir"] = LogDir;

            ConfigureLogFileTarget(_config);
            ConfigureLoggingRules(_config);

            LogManager.GlobalThreshold = LogLevel.Trace;
            LogManager.Configuration = _config;

            AttachTraceListenerIdempotent();
            LogManager.ReconfigExistingLoggers();
        }

        internal void Shutdown()
        {
            Trace.WriteLine($"{nameof(Shutdown)}", GetType().FullName);
            try
            {
                LogManager.Flush(); // Ensure all logs are written
                LogManager.Shutdown();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{ex}", GetType().FullName);
            }
        }

        private void ConfigureLoggingRules(LoggingConfiguration config)
        {
            // Add a rule for all loggers to the async file target
            var asyncWrapper = config.FindTargetByName(FileTargetName);
            if (asyncWrapper != null)
            {
                config.AddRule(LogLevel.Trace, LogLevel.Fatal, asyncWrapper);
            }
        }

        private void ConfigureLogFileTarget(LoggingConfiguration config)
        {
            // IMPORTANT:
            // ArchiveFileName + ArchiveEvery works best when FileName is static (not ${shortdate}).
            // We'll keep ONE active file and archive it into dated files. :contentReference[oaicite:3]{index=3}

            var fileTarget = new FileTarget
            {
                Name = FileTargetName + "_file",

                // Active file is static
                FileName = "${var:logDir}/${var:appName}_latest.log",

                Encoding = Encoding.UTF8,
                CreateDirs = true,

                // Archives carry the date + time + sequence
                ArchiveFileName = "${var:logDir}/${var:appName}_${date:format=yyyy-MM-dd}_${date:format=HH-mm-ss}.txt",
                ArchiveSuffixFormat = "_{#}",
                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveOldFileOnStartup = true,

                // NOTE: This is 5 MB (comment was incorrect before)
                ArchiveAboveSize = 5 * 1024 * 1024,
                MaxArchiveFiles = 100,

                Layout = GetUnifiedLayout()
            };

            var bufferingTarget = new BufferingTargetWrapper(fileTarget)
            {
                Name = FileTargetName + "_buffer",
                BufferSize = 100,
                FlushTimeout = 1000
            };

            var asyncWrapper = new AsyncTargetWrapper(bufferingTarget)
            {
                Name = FileTargetName,
                QueueLimit = 10000,
                OverflowAction = AsyncTargetWrapperOverflowAction.Block
            };

            config.AddTarget(asyncWrapper);
        }

        private string GetUnifiedLayout()
        {
            return
                "${date:format=yyyy-MM-dd HH\\:mm\\:ss.fff} " +
                "${var:version} ${machinename} ${processid} ${threadid} " +
                "${message}" +
                "${onexception:inner=${newline}${exception:format=tostring}}";
        }

        private void AttachTraceListenerIdempotent()
        {
            Trace.WriteLine($"{nameof(AttachTraceListenerIdempotent)}", typeof(Log).FullName);

            if (Debugger.IsAttached)
            {
                for (int i = Trace.Listeners.Count - 1; i >= 0; i--)
                {
                    if (Trace.Listeners[i] is DefaultTraceListener)
                        Trace.Listeners.RemoveAt(i);
                }
            }

            for (int i = 0; i < Trace.Listeners.Count; i++)
            {
                if (Trace.Listeners[i] is NLogTraceListener)
                    return;
            }

            try
            {
                Trace.Listeners.Add(new NLogTraceListener());
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, GetType().FullName);
            }
        }
    }
}
