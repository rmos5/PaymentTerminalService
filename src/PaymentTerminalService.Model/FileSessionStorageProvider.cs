using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// File-based implementation of <see cref="ISessionStorageProvider"/> using JSON serialization.
    /// Sessions are written as indented JSON files under <see cref="sessionDirectory"/>, which is
    /// created and marked hidden on first use.
    /// <para>
    /// File naming is delegated to <see cref="ISessionNameProvider"/>:
    /// <list type="table">
    /// <listheader><term>State</term><term>Example file name</term></listheader>
    /// <item><term>Ongoing</term><description><c>_2026-03-05_14-22-10.123.ptss</c></description></item>
    /// <item><term>Completed</term><description><c>2026-03-05_14-22-10.123.ptss</c></description></item>
    /// <item><term>Failed</term><description><c>2026-03-05_14-22-10.123.failed.ptss</c></description></item>
    /// <item><term>Confirmed</term><description><c>2026-03-05_14-22-10.123.confirmed.ptss</c></description></item>
    /// </list>
    /// Transitions between states use <see cref="File.Move"/> where the content is unchanged,
    /// and write + delete where new status entries are added.
    /// </para>
    /// </summary>
    public sealed class FileSessionStorageProvider : ISessionStorageProvider
    {
        private readonly string sessionDirectory;
        private readonly ISessionNameProvider sessionNameProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSessionStorageProvider"/> class.
        /// Creates the session directory if it does not exist and marks it as hidden.
        /// </summary>
        /// <param name="sessionDirectory">Directory path where session files are stored.</param>
        /// <param name="sessionNameProvider">Provider used to derive session file names across lifecycle states.</param>
        public FileSessionStorageProvider(string sessionDirectory, ISessionNameProvider sessionNameProvider)
        {
            if (string.IsNullOrWhiteSpace(sessionDirectory))
                throw new ArgumentNullException(nameof(sessionDirectory));

            Path.GetFullPath(sessionDirectory); // Validate path format

            if (sessionNameProvider == null)
                throw new ArgumentNullException(nameof(sessionNameProvider));

            this.sessionNameProvider = sessionNameProvider;
            this.sessionDirectory = sessionDirectory;

            if (!Directory.Exists(this.sessionDirectory))
            {
                Directory.CreateDirectory(this.sessionDirectory);
            }

            var attributes = File.GetAttributes(this.sessionDirectory);
            if ((attributes & FileAttributes.Hidden) == 0)
            {
                File.SetAttributes(this.sessionDirectory, attributes | FileAttributes.Hidden);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Assigns a session name when <paramref name="item"/> does not already have one.
        /// The assigned name is written back to <paramref name="item"/> so the caller retains it
        /// for subsequent saves.
        /// <para>
        /// For in-progress sessions the file is written under the ongoing file name.
        /// When the latest status is final the session content is written under the completed or
        /// failed file name and the ongoing file is moved away using <see cref="File.Move"/>.
        /// </para>
        /// </remarks>
        public void SaveSession(TerminalSessionResponse item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            item.SessionName = item.SessionName?.Trim();
            item.SessionName = sessionNameProvider.GetOrCreateSessionName(item.SessionName);

            var latestStatus = GetLatestStatus(item);
            var isFinal = latestStatus != null && latestStatus.LastResultIsFinal;

            string fileName;
            if (isFinal)
            {
                bool isAborted = latestStatus.State == TerminalState.Aborted;
                fileName = (latestStatus.Fault == null || isAborted)
                    ? sessionNameProvider.GetCompletedSessionFileName(item.SessionName)
                    : sessionNameProvider.GetFailedSessionFileName(item.SessionName);
            }
            else
            {
                fileName = sessionNameProvider.GetOngoingSessionFileName(item.SessionName);
            }

            var filePath = GetSessionFilePath(fileName);
            var payload = JsonConvert.SerializeObject(item, Formatting.Indented);

            File.WriteAllText(filePath, payload);

            if (isFinal)
            {
                MoveOngoingFile(item.SessionName, filePath);
            }
        }

        /// <inheritdoc/>
        public void ConfirmSession(string sessionName)
        {
            if (string.IsNullOrWhiteSpace(sessionName))
                throw new ArgumentNullException(nameof(sessionName));

            var sourcePath = GetSessionFilePath(sessionNameProvider.GetCompletedSessionFileName(sessionName));

            if (!File.Exists(sourcePath))
                throw new KeyNotFoundException($"No completed session file found for session '{sessionName}'.");

            var destPath = GetSessionFilePath(sessionNameProvider.GetConfirmedSessionFileName(sessionName));

            File.Move(sourcePath, destPath);
        }

        /// <inheritdoc/>
        public IEnumerable<TerminalSessionResponse> LoadOngoingSessions()
        {
            return LoadSessionsFromDirectory(
                TimestampFileNameProvider.OngoingPrefix + "*" + TimestampFileNameProvider.FileExtension);
        }

        /// <inheritdoc/>
        public IEnumerable<TerminalSessionResponse> LoadCompletedSessions()
        {
            return LoadSessionsFromDirectory(
                "*" + TimestampFileNameProvider.FileExtension,
                excludePrefix: TimestampFileNameProvider.OngoingPrefix,
                excludeSuffix: TimestampFileNameProvider.FailedSuffix + TimestampFileNameProvider.FileExtension,
                excludeSuffix2: TimestampFileNameProvider.ConfirmedSuffix + TimestampFileNameProvider.FileExtension);
        }

        /// <inheritdoc/>
        public IEnumerable<TerminalSessionResponse> LoadFailedSessions()
        {
            return LoadSessionsFromDirectory(
                "*" + TimestampFileNameProvider.FailedSuffix + TimestampFileNameProvider.FileExtension);
        }

        /// <inheritdoc/>
        public IEnumerable<TerminalSessionResponse> LoadConfirmedSessions()
        {
            return LoadSessionsFromDirectory(
                "*" + TimestampFileNameProvider.ConfirmedSuffix + TimestampFileNameProvider.FileExtension);
        }

        private IEnumerable<TerminalSessionResponse> LoadSessionsFromDirectory(
            string searchPattern,
            string excludePrefix = null,
            string excludeSuffix = null,
            string excludeSuffix2 = null)
        {
            var files = Directory.GetFiles(sessionDirectory, searchPattern);
            var results = new List<TerminalSessionResponse>(files.Length);

            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);

                if (excludePrefix != null && fileName.StartsWith(excludePrefix))
                    continue;

                if (excludeSuffix != null && fileName.EndsWith(excludeSuffix))
                    continue;

                if (excludeSuffix2 != null && fileName.EndsWith(excludeSuffix2))
                    continue;

                try
                {
                    var payload = File.ReadAllText(filePath);
                    var session = JsonConvert.DeserializeObject<TerminalSessionResponse>(payload);

                    if (session != null)
                        results.Add(session);
                }
                catch (Exception)
                {
                    // Skip unreadable or malformed session files.
                }
            }

            return results;
        }

        /// <summary>
        /// Deletes the ongoing session file once the final content has been written under
        /// the completed or failed file name. If no ongoing file exists the call is a no-op.
        /// </summary>
        /// <param name="sessionName">The bare session name.</param>
        /// <param name="finalFilePath">The already-written final file path (unused, retained for call-site clarity).</param>
        private void MoveOngoingFile(string sessionName, string finalFilePath)
        {
            var ongoingFilePath = GetSessionFilePath(sessionNameProvider.GetOngoingSessionFileName(sessionName));

            if (File.Exists(ongoingFilePath))
                File.Delete(ongoingFilePath);
        }

        private string GetSessionFilePath(string fileName)
        {
            return Path.Combine(sessionDirectory, fileName);
        }

        private static TerminalStatus GetLatestStatus(TerminalSessionResponse sessionResponse)
        {
            if (sessionResponse.Statuses == null || sessionResponse.Statuses.Count == 0)
                return null;

            foreach (var status in sessionResponse.Statuses)
                return status;

            return null;
        }
    }
}