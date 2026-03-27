using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Provides a thread-safe, configurable polling mechanism for asynchronously retrieving terminal status.
    /// Supports polling interval, maximum poll count, timeout, and terminal session completion notifications.
    /// Intended for use cases such as periodically querying device or service status in the background.
    /// </summary>
    public class TerminalStatusPoller : IDisposable
    {
        private readonly Func<CancellationToken, Task<TerminalStatus>> pollFunc;
        private readonly object sync = new object();
        private bool disposed;
        private TimeSpan interval;
        private int? maxPollCount;
        private TimeSpan? timeout;

        private CancellationTokenSource cts;
        private Task pollTask;
        private TerminalStatus latestStatus;

        /// <summary>
        /// Occurs when a new status is received from polling.
        /// For the final event in a polling session, <see cref="TerminalStatusEventArgs.IsLastPoll"/>
        /// is <see langword="true"/> and <see cref="TerminalStatusEventArgs.StopReason"/> describes
        /// why polling ended.
        /// </summary>
        public event EventHandler<TerminalStatusEventArgs> StatusReceived;

        /// <summary>
        /// Gets the most recently received status in a thread-safe manner.
        /// </summary>
        public TerminalStatus LatestStatus
        {
            get
            {
                lock (sync)
                {
                    return latestStatus;
                }
            }
            private set
            {
                lock (sync)
                {
                    latestStatus = value;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TerminalStatusPoller"/> class.
        /// </summary>
        /// <param name="pollFunc">The asynchronous function to poll for status.</param>
        /// <param name="interval">Polling interval.</param>
        /// <param name="maxPollCount">Optional maximum number of polls before stopping.</param>
        /// <param name="timeout">Optional timeout for the polling session.</param>
        public TerminalStatusPoller(
            Func<CancellationToken, Task<TerminalStatus>> pollFunc,
            TimeSpan interval,
            int? maxPollCount = null,
            TimeSpan? timeout = null)
        {
            this.pollFunc = pollFunc ?? throw new ArgumentNullException(nameof(pollFunc));
            this.interval = interval;
            this.maxPollCount = maxPollCount;
            this.timeout = timeout;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TerminalStatusPoller"/> class for interval and max poll count.
        /// </summary>
        /// <param name="pollFunc">The asynchronous function to poll for status.</param>
        /// <param name="interval">Polling interval.</param>
        /// <param name="maxPollCount">Maximum number of polls before stopping.</param>
        public TerminalStatusPoller(
            Func<CancellationToken, Task<TerminalStatus>> pollFunc,
            TimeSpan interval,
            int maxPollCount)
            : this(pollFunc, interval, maxPollCount, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TerminalStatusPoller"/> class for interval and timeout.
        /// </summary>
        /// <param name="pollFunc">The asynchronous function to poll for status.</param>
        /// <param name="interval">Polling interval.</param>
        /// <param name="timeout">Timeout for the polling session.</param>
        public TerminalStatusPoller(
            Func<CancellationToken, Task<TerminalStatus>> pollFunc,
            TimeSpan interval,
            TimeSpan timeout)
            : this(pollFunc, interval, null, timeout)
        {
        }

        /// <summary>
        /// Starts polling for terminal status.
        /// </summary>
        /// <param name="initialDelay">
        /// Optional delay before the first poll. Use when the caller needs to allow
        /// server-side state to advance before polling begins (e.g. after responding to a prompt).
        /// </param>
        public void Start(TimeSpan? initialDelay = null)
        {
            Trace.WriteLine($"{nameof(Start)}", GetType().FullName);

            StopInternal();

            lock (sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(TerminalStatusPoller));

                TimeSpan sessionInterval = interval;
                int? sessionMaxPollCount = maxPollCount;
                TimeSpan? sessionTimeout = timeout;
                TimeSpan sessionInitialDelay = initialDelay ?? TimeSpan.Zero;

                cts = new CancellationTokenSource();
                CancellationTokenSource sessionCancellationTokenSource = cts;
                pollTask = Task.Run(
                    () => RunPollingSessionAsync(
                        sessionCancellationTokenSource,
                        sessionInterval,
                        sessionMaxPollCount,
                        sessionTimeout,
                        sessionInitialDelay),
                    sessionCancellationTokenSource.Token);
            }
        }

        /// <summary>
        /// Stops polling if running. Safe to call multiple times; does nothing if already stopped.
        /// </summary>
        public void Stop()
        {
            Trace.WriteLine($"{nameof(Stop)}", GetType().FullName);

            StopInternal();
        }

        /// <summary>
        /// Updates polling settings. Changes take effect the next time <see cref="Start"/> is called.
        /// </summary>
        /// <param name="interval">Polling interval.</param>
        /// <param name="maxPollCount">Optional maximum number of polls.</param>
        /// <param name="timeout">Optional timeout for the polling session.</param>
        public void Configure(TimeSpan interval, int? maxPollCount = null, TimeSpan? timeout = null)
        {
            Trace.WriteLine($"{nameof(Configure)}", GetType().FullName);

            lock (sync)
            {
                this.interval = interval;
                this.maxPollCount = maxPollCount;
                this.timeout = timeout;
            }
        }

        private static bool IsCancellationAggregateException(AggregateException exception)
        {
            if (exception == null)
            {
                return false;
            }

            AggregateException flattenedException = exception.Flatten();
            if (flattenedException.InnerExceptions.Count == 0)
            {
                return false;
            }

            foreach (Exception item in flattenedException.InnerExceptions)
            {
                if (!(item is OperationCanceledException))
                {
                    return false;
                }
            }

            return true;
        }

        private static Exception CreateCompletionError(
            CancellationToken sessionToken,
            CancellationTokenSource timeoutCancellationTokenSource,
            OperationCanceledException exception)
        {
            if (timeoutCancellationTokenSource != null
                && timeoutCancellationTokenSource.IsCancellationRequested
                && !sessionToken.IsCancellationRequested)
            {
                return new TimeoutException("Terminal status polling timed out.", exception);
            }

            if (sessionToken.IsCancellationRequested)
            {
                return null;
            }

            return exception;
        }

        private static TerminalStatusPollStopReason GetCompletionStopReason(Exception error)
        {
            if (error is TimeoutException)
            {
                return TerminalStatusPollStopReason.TimeoutReached;
            }

            if (error != null)
            {
                return TerminalStatusPollStopReason.Faulted;
            }

            return TerminalStatusPollStopReason.Stopped;
        }

        private bool TryReleaseCurrentSession(CancellationTokenSource sessionCancellationTokenSource)
        {
            lock (sync)
            {
                if (!ReferenceEquals(cts, sessionCancellationTokenSource))
                {
                    return false;
                }

                cts = null;
                pollTask = null;
                return true;
            }
        }

        private async Task RunPollingSessionAsync(
            CancellationTokenSource sessionCancellationTokenSource,
            TimeSpan sessionInterval,
            int? sessionMaxPollCount,
            TimeSpan? sessionTimeout,
            TimeSpan initialDelay)
        {
            CancellationToken sessionToken = sessionCancellationTokenSource.Token;
            CancellationToken effectiveToken = sessionToken;
            CancellationTokenSource timeoutCancellationTokenSource = null;
            int count = 0;
            Exception finalError = null;
            TerminalStatusPollStopReason stopReason = TerminalStatusPollStopReason.Stopped;

            if (sessionTimeout.HasValue)
            {
                timeoutCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
                timeoutCancellationTokenSource.CancelAfter(sessionTimeout.Value);
                effectiveToken = timeoutCancellationTokenSource.Token;
            }

            try
            {
                if (initialDelay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(initialDelay, effectiveToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        finalError = CreateCompletionError(sessionToken, timeoutCancellationTokenSource, ex);
                        stopReason = finalError is TimeoutException
                            ? TerminalStatusPollStopReason.TimeoutReached
                            : TerminalStatusPollStopReason.Stopped;

                        if (TryReleaseCurrentSession(sessionCancellationTokenSource))
                        {
                            sessionCancellationTokenSource.Dispose();
                            OnStatusReceived(LatestStatus, true, finalError, stopReason);
                        }

                        return;
                    }
                }

                while (!effectiveToken.IsCancellationRequested &&
                       (!sessionMaxPollCount.HasValue || count < sessionMaxPollCount.Value))
                {
                    TerminalStatus status;
                    try
                    {
                        status = await pollFunc(effectiveToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        finalError = CreateCompletionError(sessionToken, timeoutCancellationTokenSource, ex);
                        stopReason = finalError is TimeoutException
                            ? TerminalStatusPollStopReason.TimeoutReached
                            : TerminalStatusPollStopReason.Stopped;
                        break;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Polling error: {ex}", GetType().FullName);
                        finalError = ex;
                        stopReason = TerminalStatusPollStopReason.Faulted;
                        break;
                    }

                    LatestStatus = status;
                    count++;

                    if (!sessionMaxPollCount.HasValue || count < sessionMaxPollCount.Value)
                    {
                        OnStatusReceived(status, false);
                    }

                    if (sessionMaxPollCount.HasValue && count >= sessionMaxPollCount.Value)
                    {
                        stopReason = TerminalStatusPollStopReason.MaxPollCountReached;
                        break;
                    }

                    try
                    {
                        await Task.Delay(sessionInterval, effectiveToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException ex)
                    {
                        finalError = CreateCompletionError(sessionToken, timeoutCancellationTokenSource, ex);
                        stopReason = finalError is TimeoutException
                            ? TerminalStatusPollStopReason.TimeoutReached
                            : TerminalStatusPollStopReason.Stopped;
                        break;
                    }
                }
            }
            finally
            {
                timeoutCancellationTokenSource?.Dispose();
            }

            if (!TryReleaseCurrentSession(sessionCancellationTokenSource))
            {
                return;
            }

            sessionCancellationTokenSource.Dispose();
            OnStatusReceived(LatestStatus, true, finalError, stopReason);
        }

        private void StopInternal(Exception error = null)
        {
            CancellationTokenSource sessionCancellationTokenSource;
            Task sessionPollTask;

            lock (sync)
            {
                if (cts == null)
                {
                    return;
                }

                sessionCancellationTokenSource = cts;
                sessionPollTask = pollTask;
                cts = null;
                pollTask = null;
            }

            sessionCancellationTokenSource.Cancel();

            try
            {
                if (sessionPollTask != null
                    && (!Task.CurrentId.HasValue || sessionPollTask.Id != Task.CurrentId.Value))
                {
                    sessionPollTask.Wait();
                }
            }
            catch (AggregateException ex)
            {
                if (!IsCancellationAggregateException(ex))
                {
                    Trace.WriteLine($"Polling task completion error ignored during stop: {ex}", GetType().FullName);

                    if (error == null)
                    {
                        error = ex.InnerExceptions.Count == 1 ? ex.InnerExceptions[0] : ex;
                    }
                }
            }
            finally
            {
                sessionCancellationTokenSource.Dispose();
            }

            OnStatusReceived(LatestStatus, true, error, GetCompletionStopReason(error));
        }

        /// <summary>
        /// Raises the <see cref="StatusReceived"/> event.
        /// </summary>
        /// <param name="status">The terminal status associated with the event.</param>
        /// <param name="isLastPoll">Indicates whether this is the final event for the current polling session.</param>
        /// <param name="error">Optional error encountered during polling.</param>
        /// <param name="stopReason">
        /// The reason polling ended. Use <see cref="TerminalStatusPollStopReason.None"/> for non-final events.
        /// </param>
        protected virtual void OnStatusReceived(
            TerminalStatus status,
            bool isLastPoll,
            Exception error = null,
            TerminalStatusPollStopReason stopReason = TerminalStatusPollStopReason.None)
        {
            StatusReceived?.Invoke(this, new TerminalStatusEventArgs(status, isLastPoll, error, stopReason));
        }

        /// <summary>
        /// Disposes the poller and stops polling if running. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            Trace.WriteLine($"{nameof(Dispose)}", GetType().FullName);

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the resources used by the poller.
        /// </summary>
        /// <param name="disposing">True to dispose managed resources; otherwise, false.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                lock (sync)
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                }

                StopInternal();
                return;
            }

            disposed = true;
        }
    }

    /// <summary>
    /// Event arguments for terminal status polling events.
    /// Contains the polled <see cref="TerminalStatus"/>, a flag indicating whether the event is the
    /// final event for the current polling session, an optional error, and the reason polling ended.
    /// </summary>
    public class TerminalStatusEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the terminal status associated with the event.
        /// For a final event, this is the latest status known by the poller.
        /// </summary>
        public TerminalStatus Status { get; }

        /// <summary>
        /// Gets a value indicating whether this event is the final event
        /// for the current polling session.
        /// </summary>
        public bool IsLastPoll { get; }

        /// <summary>
        /// Gets the error encountered during polling, if any.
        /// This is typically set only for faulted final events.
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Gets the reason the polling session ended.
        /// For non-final events, this value is typically <see cref="TerminalStatusPollStopReason.None"/>.
        /// </summary>
        public TerminalStatusPollStopReason StopReason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TerminalStatusEventArgs"/> class.
        /// </summary>
        /// <param name="status">The terminal status associated with the event.</param>
        /// <param name="isLastPoll">True if this is the final event for the current polling session; otherwise, false.</param>
        /// <param name="error">Optional error encountered during polling.</param>
        /// <param name="stopReason">The reason the polling session ended.</param>
        public TerminalStatusEventArgs(
            TerminalStatus status,
            bool isLastPoll,
            Exception error = null,
            TerminalStatusPollStopReason stopReason = TerminalStatusPollStopReason.None)
        {
            Status = status;
            IsLastPoll = isLastPoll;
            Error = error;
            StopReason = stopReason;
        }
    }

    /// <summary>
    /// Describes why a polling session ended.
    /// </summary>
    public enum TerminalStatusPollStopReason
    {
        /// <summary>
        /// No terminal stop reason is associated with the event.
        /// Used for regular non-final status updates.
        /// </summary>
        None = 0,

        /// <summary>
        /// Polling was stopped explicitly by the caller.
        /// </summary>
        Stopped,

        /// <summary>
        /// Polling ended because the configured maximum poll count was reached.
        /// </summary>
        MaxPollCountReached,

        /// <summary>
        /// Polling ended because the configured timeout was reached.
        /// </summary>
        TimeoutReached,

        /// <summary>
        /// Polling ended because an unexpected error occurred.
        /// </summary>
        Faulted
    }
}