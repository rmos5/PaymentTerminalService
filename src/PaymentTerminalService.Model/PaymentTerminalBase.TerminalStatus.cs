using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Partial class for <see cref="PaymentTerminalBase"/> containing state transition logic and status tracking.
    /// </summary>
    public abstract partial class PaymentTerminalBase
    {
        private readonly LinkedList<TerminalStatus> sessionStatuses = new LinkedList<TerminalStatus>();
        private readonly object sessionStatusesLock = new object();
        private readonly object sessionPersistenceLock = new object();
        private TerminalStatus currentStatus;
        private TerminalStatus previousStatus;
        private bool isPaymentSessionActive;
        private string currentSessionName;

        /// <summary>
        /// Gets the most recent terminal status.
        /// </summary>
        public TerminalStatus CurrentStatus
        {
            get
            {
                lock (sessionStatusesLock)
                {
                    return currentStatus;
                }
            }
        }

        /// <summary>
        /// Gets the previous terminal status.
        /// </summary>
        public TerminalStatus PreviousStatus
        {
            get
            {
                lock (sessionStatusesLock)
                {
                    return previousStatus;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether a payment session is currently active.
        /// A session is active between StartSession() and either reaching a final state or calling EndSession().
        /// </summary>
        public bool IsPaymentSessionActive
        {
            get
            {
                lock (sessionStatusesLock)
                {
                    return isPaymentSessionActive;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether a connection test is required before the next operation.
        /// Set automatically when the terminal enters <see cref="TerminalState.Error"/>.
        /// Cleared by a successful <see cref="TestConnectionAsync"/> call.
        /// </summary>
        public bool IsTerminalTestRequired { get; private set; }

        private void EnsureCanStartOperation(OperationType operationType)
        {
            Trace.WriteLine($"{nameof(EnsureCanStartOperation)}: {operationType}", GetType().FullName);

            lock (sessionStatusesLock)
            {
                ValidateCanStartOperation(operationType, currentStatus, isPaymentSessionActive);
            }
        }

        private void ValidateCanStartOperation(OperationType operationType, TerminalStatus status, bool isSessionActive)
        {
            if (operationType == OperationType.Abort)
            {
                if (!isSessionActive && status?.ActiveOperationType == OperationType.Abort)
                    throw new ApiConflictException("Cannot abort: no active session and terminal is already idle after a previous abort.");
            }
            else if (operationType == OperationType.Purchase
                && status?.State == TerminalState.AwaitingResult
                && status?.ActiveOperationType == OperationType.LoyaltyActivate)
            {
                // Loyalty-to-purchase transition — allowed.
            }
            else if (operationType == OperationType.LoyaltyDeactivate)
            {
                if (!IsLoyaltySupported)
                    throw new NotSupportedException("Loyalty operations are not supported by this terminal.");

                if (isSessionActive)
                    throw new ApiConflictException("Cannot deactivate loyalty while a purchase session is active.");
            }
            else if (operationType == OperationType.LoyaltyActivate)
            {
                if (!IsLoyaltySupported)
                    throw new NotSupportedException("Loyalty operations are not supported by this terminal.");

                if (isSessionActive)
                    throw new ApiConflictException($"Cannot start {operationType}: a session is already active.");

                if (IsBusyState(status?.State ?? TerminalState.Idle))
                    throw new ApiConflictException($"Cannot start {operationType}: terminal is busy ({status?.State}).");
            }
            else
            {
                if (isSessionActive)
                    throw new ApiConflictException($"Cannot start {operationType}: a session is already active.");

                if (IsBusyState(status?.State ?? TerminalState.Idle))
                    throw new ApiConflictException($"Cannot start {operationType}: terminal is busy ({status?.State}).");
            }
        }

        /// <summary>
        /// Updates the current terminal status without affecting session history.
        /// Use this for non-transactional status updates (e.g., idle state, connection changes).
        /// </summary>
        /// <remarks>
        /// Exceptions are caught and traced rather than propagated, because this method is
        /// frequently called from hardware event handlers where an unhandled exception would
        /// crash the ECR protocol thread.
        /// <para>
        /// <see cref="ArgumentNullException"/> for a <see langword="null"/> <paramref name="status"/>
        /// is still thrown — a null status is always a programming error at the call site.
        /// </para>
        /// </remarks>
        /// <param name="status">The new terminal status.</param>
        protected void UpdateCurrentStatus(TerminalStatus status)
        {
            Trace.WriteLine($"{nameof(UpdateCurrentStatus)}: {status}", GetType().FullName);

            if (status == null)
                throw new ArgumentNullException(nameof(status));

            try
            {
                lock (sessionStatusesLock)
                {
                    if (Equals(status, currentStatus))
                        throw new InvalidOperationException("Attempted to set a duplicate terminal status.");

                    Trace.WriteLine($"{nameof(UpdateCurrentStatus)}: {currentStatus} => {status}", GetType().FullName);

                    previousStatus = currentStatus;
                    currentStatus = status;

                    if (status.State == TerminalState.Error)
                        IsTerminalTestRequired = true;
                    else if ((status.State == TerminalState.Idle || status.State == TerminalState.Aborted) && IsTerminalTestRequired)
                        IsTerminalTestRequired = false;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(UpdateCurrentStatus)}: swallowed exception from event handler context: {ex}", GetType().FullName);
            }
        }

        /// <summary>
        /// Starts a new transaction session, clearing any previous session history.
        /// Atomically validates that no session is already active before committing.
        /// </summary>
        /// <param name="status">The initial status for the new session.</param>
        protected void StartSession(TerminalStatus status)
        {
            Trace.WriteLine($"{nameof(StartSession)}: {status}", GetType().FullName);

            lock (sessionStatusesLock)
            {
                // Re-validate under the lock — prevents a race where two callers both pass
                // EnsureCanStartOperation before either has called StartSession.
                ValidateCanStartOperation(status.ActiveOperationType, currentStatus, isPaymentSessionActive);

                sessionStatuses.Clear();
                currentSessionName = null;
                isPaymentSessionActive = true;

                // Carry forward loyalty context as first session entry when transitioning
                // from a loyalty activation directly into a purchase.
                if (currentStatus.State == TerminalState.WaitingForUserAction
                    && currentStatus.ActiveOperationType == OperationType.LoyaltyActivate)
                {
                    sessionStatuses.AddFirst(currentStatus);
                }

                // Establish the new operation id on currentStatus before releasing the lock
                // so the stale-operation-id guard in AddSessionStatus does not discard the
                // initial status. currentStatus is set to status but sessionStatuses is still
                // empty for this session — AddSessionStatus will add it there.
                previousStatus = currentStatus;
                currentStatus = status;
            }

            AddSessionStatus(status);
        }

        /// <summary>
        /// Adds a status update to the current active session.
        /// The session must have been started with <see cref="StartSession"/>.
        /// Automatically persists the session after adding the status.
        /// </summary>
        /// <remarks>
        /// Exceptions from <see cref="SaveSession"/> and contract violations (no active session,
        /// duplicate status) are caught and traced rather than propagated, because this method is
        /// frequently called from hardware event handlers where an unhandled exception would crash
        /// the ECR protocol thread.
        /// <para>
        /// <see cref="ArgumentNullException"/> for a <see langword="null"/> <paramref name="status"/>
        /// is still thrown — a null status is always a programming error at the call site.
        /// </para>
        /// </remarks>
        /// <param name="status">The new terminal status.</param>
        protected void AddSessionStatus(TerminalStatus status)
        {
            Trace.WriteLine($"{nameof(AddSessionStatus)}: {status}", GetType().FullName);

            try
            {
                lock (sessionStatusesLock)
                {
                    if (!isPaymentSessionActive)
                        throw new InvalidOperationException("Cannot add session status when no session is active. Call StartSession first.");

                    // Allow the initial status added by StartSession through even though it is
                    // already currentStatus — it has not been recorded in sessionStatuses yet.
                    bool isInitialSessionStatus = ReferenceEquals(status, currentStatus)
                        && sessionStatuses.Count == 0;

                    if (!isInitialSessionStatus && Equals(status, currentStatus))
                        throw new InvalidOperationException("Attempted to add a duplicate terminal status.");

                    // Discard stale results from a previous session delivered after a new session started.
                    // The operation ID on the incoming status must match the active session's operation ID.
                    if (status.ActiveOperationId != null
                        && currentStatus.ActiveOperationId != null
                        && !string.Equals(status.ActiveOperationId, currentStatus.ActiveOperationId, StringComparison.Ordinal))
                    {
                        Trace.WriteLine($"{nameof(AddSessionStatus)}: discarding stale status for operation '{status.ActiveOperationId}', active is '{currentStatus.ActiveOperationId}'.", GetType().FullName);
                        return;
                    }

                    if (!isInitialSessionStatus)
                    {
                        Trace.WriteLine($"{nameof(AddSessionStatus)}: {currentStatus} => {status}", GetType().FullName);
                        previousStatus = currentStatus;
                        currentStatus = status;
                    }
                    else
                    {
                        Trace.WriteLine($"{nameof(AddSessionStatus)}: {previousStatus} => {status} (initial)", GetType().FullName);
                    }

                    sessionStatuses.AddFirst(status);

                    if (status.State == TerminalState.Error)
                        IsTerminalTestRequired = true;
                    else if ((status.State == TerminalState.Idle || status.State == TerminalState.Aborted) && IsTerminalTestRequired)
                        IsTerminalTestRequired = false;

                    if (!IsBusyState(status.State) && status.LastResultIsFinal)
                    {
                        Trace.WriteLine($"{nameof(AddSessionStatus)}: Session ended with final state {status.State}", GetType().FullName);
                        isPaymentSessionActive = false;
                        abortStartedAt = null;
                    }
                }

                SaveSession();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(AddSessionStatus)}: swallowed exception from event handler context: {ex}", GetType().FullName);
            }
        }

        /// <summary>
        /// Ends the current session without adding a new status.
        /// Use this when aborting or releasing a terminal mid-transaction.
        /// </summary>
        protected void EndSession()
        {
            Trace.WriteLine($"{nameof(EndSession)}", GetType().FullName);

            lock (sessionStatusesLock)
            {
                if (isPaymentSessionActive)
                    isPaymentSessionActive = false;
            }
        }

        private void SaveSession()
        {
            Trace.WriteLine($"{nameof(SaveSession)}", GetType().FullName);

            string sessionName;

            lock (sessionPersistenceLock)
            {
                var sessionResponse = CreateTerminalSessionResponse();
                SessionStorageProvider.SaveSession(sessionResponse);
                currentSessionName = sessionResponse.SessionName;
                sessionName = currentSessionName;
            }

            lock (sessionStatusesLock)
            {
                if (currentStatus != null && sessionName != null)
                    currentStatus.SessionName = sessionName;
            }
        }

        /// <summary>
        /// Returns a thread-safe snapshot of session statuses ordered newest to oldest.
        /// </summary>
        protected TerminalStatus[] GetSessionStatuses()
        {
            lock (sessionStatusesLock)
            {
                var statuses = new List<TerminalStatus>(sessionStatuses.Count);

                foreach (var status in sessionStatuses)
                    statuses.Add(status);

                return statuses.ToArray();
            }
        }

        /// <summary>
        /// Creates the terminal session response from the currently tracked session statuses.
        /// </summary>
        protected virtual TerminalSessionResponse CreateTerminalSessionResponse()
        {
            Trace.WriteLine($"{nameof(CreateTerminalSessionResponse)}", GetType().FullName);

            return new TerminalSessionResponse
            {
                SessionName = currentSessionName,
                Statuses = GetSessionStatuses()
            };
        }

        /// <summary>
        /// Loads all ongoing sessions from the configured storage provider.
        /// Use on startup to detect and recover sessions interrupted by a process restart.
        /// </summary>
        /// <returns>All ongoing session responses found in storage.</returns>
        protected IEnumerable<TerminalSessionResponse> LoadOngoingSessions()
        {
            Trace.WriteLine($"{nameof(LoadOngoingSessions)}", GetType().FullName);

            lock (sessionPersistenceLock)
            {
                return SessionStorageProvider.LoadOngoingSessions();
            }
        }

        /// <summary>
        /// Loads all successfully completed sessions from the configured storage provider.
        /// </summary>
        /// <returns>All completed session responses found in storage.</returns>
        protected IEnumerable<TerminalSessionResponse> LoadCompletedSessions()
        {
            Trace.WriteLine($"{nameof(LoadCompletedSessions)}", GetType().FullName);

            lock (sessionPersistenceLock)
            {
                return SessionStorageProvider.LoadCompletedSessions();
            }
        }

        /// <summary>
        /// Loads all failed sessions from the configured storage provider.
        /// Failed sessions reached a final state with a fault and may require manual reconciliation.
        /// </summary>
        /// <returns>All failed session responses found in storage.</returns>
        protected IEnumerable<TerminalSessionResponse> LoadFailedSessions()
        {
            Trace.WriteLine($"{nameof(LoadFailedSessions)}", GetType().FullName);

            lock (sessionPersistenceLock)
            {
                return SessionStorageProvider.LoadFailedSessions();
            }
        }
    }
}