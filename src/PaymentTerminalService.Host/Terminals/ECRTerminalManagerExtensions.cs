using System;
using System.Threading;
using System.Threading.Tasks;

namespace Verifone.ECRTerminal
{
    /// <summary>
    /// Provides extension methods for <see cref="IECRTerminalManager"/>.
    /// </summary>
    internal static class ECRTerminalManagerExtensions
    {
        /// <summary>
        /// Sends a command to the terminal and asynchronously waits until the terminal acknowledges
        /// receipt of the command (ACK) before returning.
        /// </summary>
        /// <param name="terminalManager">The <see cref="IECRTerminalManager"/> to send the command through.</param>
        /// <param name="commandId">
        /// The unique identifier of the command to wait for. Only acknowledgements matching this
        /// identifier will satisfy the wait.
        /// </param>
        /// <param name="sendCommand">
        /// A delegate that sends the command to the terminal. Invoked after event subscriptions
        /// are established to avoid missing the acknowledgement.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that can be used to cancel the wait. If cancelled, a
        /// <see cref="OperationCanceledException"/> is thrown.
        /// </param>
        /// <returns>A <see cref="Task"/> that completes when the terminal acknowledges the command.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="terminalManager"/>, <paramref name="commandId"/>, or
        /// <paramref name="sendCommand"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when <paramref name="cancellationToken"/> is cancelled before the terminal responds.
        /// </exception>
        public static async Task WaitForCommandAcceptanceAsync(
            this IECRTerminalManager terminalManager,
            string commandId,
            Action sendCommand,
            CancellationToken cancellationToken)
        {
            if (terminalManager == null)
                throw new ArgumentNullException(nameof(terminalManager));
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentNullException(nameof(commandId));
            if (sendCommand == null)
                throw new ArgumentNullException(nameof(sendCommand));

            cancellationToken.ThrowIfCancellationRequested();

            var completionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<TerminalCommandAcceptedEventArgs> acceptedHandler = null;
            EventHandler<ExceptionEventArgs> errorHandler = null;
            CancellationTokenRegistration cancellationRegistration = default;

            acceptedHandler = (sender, eventArgs) =>
            {
                if (!string.Equals(eventArgs.CommandId, commandId, StringComparison.Ordinal))
                {
                    return;
                }

                completionSource.TrySetResult(true);
            };

            errorHandler = (sender, eventArgs) =>
            {
                completionSource.TrySetException(eventArgs.Exception);
            };

            try
            {
                terminalManager.TerminalCommandAccepted += acceptedHandler;
                terminalManager.TerminalError += errorHandler;

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));
                }

                sendCommand();

                await completionSource.Task.ConfigureAwait(false);
            }
            finally
            {
                terminalManager.TerminalCommandAccepted -= acceptedHandler;
                terminalManager.TerminalError -= errorHandler;
                cancellationRegistration.Dispose();
            }
        }

        /// <summary>
        /// Sends a command to the terminal, waits for the terminal to acknowledge the command,
        /// and then asynchronously waits for a result event that satisfies an optional filter.
        /// </summary>
        /// <typeparam name="TResultEventArgs">
        /// The type of <see cref="EventArgs"/> raised when the terminal produces a result.
        /// </typeparam>
        /// <param name="terminalManager">The <see cref="IECRTerminalManager"/> to send the command through.</param>
        /// <param name="commandId">
        /// The unique identifier of the command to correlate. Only acknowledgements matching this
        /// identifier are considered.
        /// </param>
        /// <param name="sendCommand">
        /// A delegate that sends the command to the terminal. Invoked after event subscriptions
        /// are established to avoid missing events.
        /// </param>
        /// <param name="subscribeResultHandler">
        /// A delegate that subscribes the provided result handler to the appropriate terminal event.
        /// </param>
        /// <param name="unsubscribeResultHandler">
        /// A delegate that unsubscribes the provided result handler from the terminal event.
        /// Called in the <c>finally</c> block to guarantee cleanup.
        /// </param>
        /// <param name="resultFilter">
        /// An optional predicate applied to each result event. When supplied, only events for which
        /// the predicate returns <c>true</c> complete the wait. Pass <c>null</c> to accept the first
        /// result event regardless of its content.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that can be used to cancel the wait. If cancelled, a
        /// <see cref="OperationCanceledException"/> is thrown.
        /// </param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> that resolves to the <typeparamref name="TResultEventArgs"/>
        /// instance raised by the terminal when the operation completes.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="terminalManager"/>, <paramref name="commandId"/>,
        /// <paramref name="sendCommand"/>, <paramref name="subscribeResultHandler"/>, or
        /// <paramref name="unsubscribeResultHandler"/> is <c>null</c> or empty.
        /// </exception>
        /// <exception cref="OperationCanceledException">
        /// Thrown when <paramref name="cancellationToken"/> is cancelled before the terminal responds.
        /// </exception>
        public static async Task<TResultEventArgs> WaitForCommandResultAsync<TResultEventArgs>(
            this IECRTerminalManager terminalManager,
            string commandId,
            Action sendCommand,
            Action<EventHandler<TResultEventArgs>> subscribeResultHandler,
            Action<EventHandler<TResultEventArgs>> unsubscribeResultHandler,
            Func<TResultEventArgs, bool> resultFilter,
            CancellationToken cancellationToken)
            where TResultEventArgs : EventArgs
        {
            if (terminalManager == null)
                throw new ArgumentNullException(nameof(terminalManager));
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentNullException(nameof(commandId));
            if (sendCommand == null)
                throw new ArgumentNullException(nameof(sendCommand));
            if (subscribeResultHandler == null)
                throw new ArgumentNullException(nameof(subscribeResultHandler));
            if (unsubscribeResultHandler == null)
                throw new ArgumentNullException(nameof(unsubscribeResultHandler));

            cancellationToken.ThrowIfCancellationRequested();

            var resultCompletionSource = new TaskCompletionSource<TResultEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<TerminalCommandAcceptedEventArgs> acceptedHandler = null;
            EventHandler<TResultEventArgs> resultHandler = null;
            EventHandler<ExceptionEventArgs> errorHandler = null;
            CancellationTokenRegistration cancellationRegistration = default;

            acceptedHandler = (sender, eventArgs) =>
            {
                if (!string.Equals(eventArgs.CommandId, commandId, StringComparison.Ordinal))
                {
                    return;
                }
            };

            resultHandler = (sender, eventArgs) =>
            {
                if (resultFilter != null && !resultFilter(eventArgs))
                {
                    return;
                }

                resultCompletionSource.TrySetResult(eventArgs);
            };

            errorHandler = (sender, eventArgs) =>
            {
                resultCompletionSource.TrySetException(eventArgs.Exception);
            };

            try
            {
                terminalManager.TerminalCommandAccepted += acceptedHandler;
                terminalManager.TerminalError += errorHandler;
                subscribeResultHandler(resultHandler);

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(() => resultCompletionSource.TrySetCanceled(cancellationToken));
                }

                sendCommand();

                return await resultCompletionSource.Task.ConfigureAwait(false);
            }
            finally
            {
                unsubscribeResultHandler(resultHandler);
                terminalManager.TerminalCommandAccepted -= acceptedHandler;
                terminalManager.TerminalError -= errorHandler;
                cancellationRegistration.Dispose();
            }
        }
    }
}