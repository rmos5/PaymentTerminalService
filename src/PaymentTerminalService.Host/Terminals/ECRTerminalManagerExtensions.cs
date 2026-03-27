using System;
using System.Threading;
using System.Threading.Tasks;

namespace Verifone.ECRTerminal
{
    internal static class ECRTerminalManagerExtensions
    {
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