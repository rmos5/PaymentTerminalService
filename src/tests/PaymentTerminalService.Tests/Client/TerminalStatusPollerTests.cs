using Microsoft.VisualStudio.TestTools.UnitTesting;
using PaymentTerminalService.Client;
using PaymentTerminalService.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PaymentTerminalService.Tests.Client
{
    [TestClass]
    public class TerminalStatusPollerTests
    {
        private static TerminalStatus MakeStatus(bool isFinal) => new TerminalStatus
        {
            State = isFinal ? TerminalState.Idle : TerminalState.TransactionInProgress,
            LastResultIsFinal = isFinal,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        [TestMethod]
        public async Task FinalState_FiresExactlyOneEvent_MarkedAsLastPoll()
        {
            var events = new List<TerminalStatusEventArgs>();
            var finalStatus = MakeStatus(isFinal: true);

            using (var poller = new TerminalStatusPoller(
                _ => Task.FromResult(finalStatus),
                interval: TimeSpan.FromSeconds(30)))
            {
                poller.StatusReceived += (_, e) => events.Add(e);
                poller.Start();

                await WaitForFinalEventAsync(events);
            }

            Assert.AreEqual(1, events.Count, "Expected exactly one event for a final status.");
            Assert.IsTrue(events[0].IsLastPoll);
            Assert.AreEqual(TerminalStatusPollStopReason.FinalStateReached, events[0].StopReason);
            Assert.AreSame(finalStatus, events[0].Status);
        }

        [TestMethod]
        public async Task NonFinalState_FiresNonFinalEvent_PollingContinues()
        {
            int pollCount = 0;
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (var poller = new TerminalStatusPoller(
                _ => Task.FromResult(MakeStatus(isFinal: false)),
                interval: TimeSpan.FromMilliseconds(50)))
            {
                poller.StatusReceived += (_, e) =>
                {
                    if (!e.IsLastPoll)
                    {
                        pollCount++;
                        if (pollCount >= 3)
                            tcs.TrySetResult(true);
                    }
                };

                poller.Start();
                await Task.WhenAny(tcs.Task, Task.Delay(3000));
            }

            Assert.IsTrue(pollCount >= 3, "Expected poller to keep polling for non-final status.");
        }

        [TestMethod]
        public async Task FinalState_NoNonFinalEventFired()
        {
            var nonFinalEvents = new List<TerminalStatusEventArgs>();
            var finalStatus = MakeStatus(isFinal: true);

            using (var poller = new TerminalStatusPoller(
                _ => Task.FromResult(finalStatus),
                interval: TimeSpan.FromSeconds(30)))
            {
                poller.StatusReceived += (_, e) =>
                {
                    if (!e.IsLastPoll)
                        nonFinalEvents.Add(e);
                };

                poller.Start();
                await WaitForFinalEventAsync(poller);
            }

            Assert.AreEqual(0, nonFinalEvents.Count, "No non-final event should fire for a final status.");
        }

        [TestMethod]
        public async Task ExplicitStop_FiresFinalEvent_WithStoppedReason()
        {
            var events = new List<TerminalStatusEventArgs>();

            using (var poller = new TerminalStatusPoller(
                _ => Task.FromResult(MakeStatus(isFinal: false)),
                interval: TimeSpan.FromMilliseconds(50)))
            {
                poller.StatusReceived += (_, e) => events.Add(e);
                poller.Start();

                // Wait for at least one non-final event before stopping.
                await WaitForNonFinalEventAsync(events);
                poller.Stop();
            }

            var last = events[events.Count - 1];
            Assert.IsTrue(last.IsLastPoll);
            Assert.AreEqual(TerminalStatusPollStopReason.Stopped, last.StopReason);
        }

        [TestMethod]
        public async Task NonFinalThenFinal_FinalEventIsLast_StopReasonIsFinalStateReached()
        {
            int callCount = 0;
            var events = new List<TerminalStatusEventArgs>();

            using (var poller = new TerminalStatusPoller(
                _ => Task.FromResult(MakeStatus(isFinal: ++callCount >= 3)),
                interval: TimeSpan.FromMilliseconds(50)))
            {
                poller.StatusReceived += (_, e) => events.Add(e);
                poller.Start();

                await WaitForFinalEventAsync(poller);
            }

            var last = events[events.Count - 1];
            Assert.IsTrue(last.IsLastPoll);
            Assert.IsTrue(last.Status.LastResultIsFinal);
            Assert.AreEqual(TerminalStatusPollStopReason.FinalStateReached, last.StopReason);

            // All preceding events should be non-final.
            for (int i = 0; i < events.Count - 1; i++)
                Assert.IsFalse(events[i].IsLastPoll, $"Event {i} should not be marked as last poll.");
        }

        private static async Task WaitForFinalEventAsync(List<TerminalStatusEventArgs> events, int timeoutMs = 3000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (events.Count > 0 && events[events.Count - 1].IsLastPoll)
                    return;
                await Task.Delay(10);
            }
            Assert.Fail("Timed out waiting for final event.");
        }

        private static async Task WaitForFinalEventAsync(TerminalStatusPoller poller, int timeoutMs = 3000)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            poller.StatusReceived += (_, e) => { if (e.IsLastPoll) tcs.TrySetResult(true); };
            await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        }

        private static async Task WaitForNonFinalEventAsync(List<TerminalStatusEventArgs> events, int timeoutMs = 3000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (events.Count > 0)
                    return;
                await Task.Delay(10);
            }
            Assert.Fail("Timed out waiting for first non-final event.");
        }
    }
}
