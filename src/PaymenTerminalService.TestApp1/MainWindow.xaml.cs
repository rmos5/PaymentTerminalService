using Newtonsoft.Json;
using PaymentTerminalService.Client;
using PaymentTerminalService.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WPFHelpers;

namespace PaymentTerminalService.TestApp1
{
    /// <summary>
    /// Main window for the test application.
    /// Handles UI logic for displaying, selecting, and managing payment terminals.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged, IContext
    {
        #region Commands

        /// <summary>
        /// Command for fetching terminals. Disabled while the app is busy.
        /// </summary>
        private class GetTerminalsCommandImpl : AppCommandBase<MainWindow>
        {
            public GetTerminalsCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.LoadTerminalsAsync();
            }
        }

        private abstract class SelectedTerminalCommandBase : AppCommandBase<MainWindow>
        {
            protected SelectedTerminalCommandBase(MainWindow context) : base(context) { }

            public override bool CanExecute(object parameter)
            {
                return base.CanExecute(parameter)
                    && Context.SelectedTerminal?.SelectedConnectionId != null;
            }
        }

        private class SelectTerminalCommandImpl : SelectedTerminalCommandBase
        {
            public SelectTerminalCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.SelectTerminalAsync();
            }
        }

        private class GetTerminalSettingsCommandImpl : SelectedTerminalCommandBase
        {
            public GetTerminalSettingsCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.GetTerminalSettingsAsync();
            }
        }

        private class GetTerminalStatusCommandImpl : SelectedTerminalCommandBase
        {
            public GetTerminalStatusCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.GetTerminalStatusAsync();
            }
        }

        private class GetTerminalSessionCommandImpl : SelectedTerminalCommandBase
        {
            public GetTerminalSessionCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.GetTerminalSessionAsync();
            }
        }

        private class AbortTransactionCommandImpl : SelectedTerminalCommandBase
        {
            public AbortTransactionCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.AbortTransactionAsync();
            }
        }

        private class StartPurchaseCommandImpl : SelectedTerminalCommandBase
        {
            public StartPurchaseCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.StartPurchaseAsync();
            }
        }

        private class StartReversalCommandImpl : SelectedTerminalCommandBase
        {
            public StartReversalCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.StartReversalAsync();
            }
        }

        private class StartRefundCommandImpl : SelectedTerminalCommandBase
        {
            public StartRefundCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.StartRefundAsync();
            }
        }

        private class ToggleLoyaltyCommandImpl : SelectedTerminalCommandBase
        {
            public ToggleLoyaltyCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.ToggleLoyaltyAsync();
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private PaymentTerminalServiceClient client;
        private TerminalStatusPoller terminalStatusPoller = null;

        private TraceListener TraceListener { get; set; }

        private bool isBusy;
        /// <summary>
        /// Gets a value indicating whether the application is busy (e.g., performing an operation).
        /// </summary>
        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                if (isBusy != value)
                {
                    isBusy = value;
                    UpdateModelState();
                }
            }
        }

        private decimal transactionAmount;
        /// <summary>
        /// Gets or sets the transaction amount for the current transaction.
        /// </summary>
        public decimal TransactionAmount
        {
            get => transactionAmount;
            set
            {
                if (transactionAmount != value)
                {
                    transactionAmount = value;
                    UpdateModelState();
                }
            }
        }

        private string transactionId = string.Empty;
        /// <summary>
        /// Gets or sets the transaction ID for the current transaction.
        /// </summary>
        public string TransactionId
        {
            get => transactionId;
            set
            {
                if (transactionId != value)
                {
                    transactionId = value;
                    UpdateModelState();
                }
            }
        }

        private DateTimeOffset transactionTimestamp;
        /// <summary>
        /// Gets or sets the transaction timestamp for the current transaction.
        /// </summary>
        public DateTimeOffset TransactionTimestamp
        {
            get => transactionTimestamp;
            set
            {
                if (transactionTimestamp != value)
                {
                    transactionTimestamp = value;
                    UpdateModelState();
                }
            }
        }

        private string clientReference = string.Empty;
        /// <summary>
        /// Gets or sets the client reference for the current transaction.
        /// </summary>
        public string ClientReference
        {
            get => clientReference;
            set
            {
                if (clientReference != value)
                {
                    clientReference = value;
                    UpdateModelState();
                }
            }
        }

        private bool isLoyaltyHandled;
        /// <summary>
        /// Gets or sets a value indicating whether loyalty is already processed for the current transaction.
        /// </summary>
        public bool IsLoyaltyHandled
        {
            get => isLoyaltyHandled;
            set
            {
                if (isLoyaltyHandled != value)
                {
                    isLoyaltyHandled = value;
                    if (isLoyaltyHandled)
                    {
                        IsLoyaltyActive = false;
                    }

                    UpdateModelState();
                }
            }
        }

        private bool isLoyaltyAutoReply = true;

        public bool IsLoyaltyAutoRepy
        {
            get => isLoyaltyAutoReply;
            set
            {
                if (isLoyaltyAutoReply != value)
                {
                    isLoyaltyAutoReply = value;
                    UpdateModelState();
                }
            }
        }

        public IList<string> LogMessages { get; } = new BoundedObservableCollection<string>(20, BoundedObservableCollection<string>.UpdateMode.Insert);

        private IList<TerminalDescriptorContext> paymentTerminals = new BoundedObservableCollection<TerminalDescriptorContext>(10);
        /// <summary>
        /// Gets the collection of payment terminals for UI binding.
        /// </summary>
        public IEnumerable<TerminalDescriptorContext> PaymentTerminals => paymentTerminals;

        /// <summary>
        /// Gets a value indicating whether any terminals are available.
        /// </summary>
        public bool HasTerminals => paymentTerminals.Count > 0;

        private TerminalDescriptorContext selectedTerminal;
        /// <summary>
        /// Gets or sets the currently selected terminal.
        /// </summary>
        public TerminalDescriptorContext SelectedTerminal
        {
            get => selectedTerminal;
            set
            {
                if (selectedTerminal != value)
                {
                    if (selectedTerminal != null)
                    {
                        selectedTerminal.PropertyChanged -= SelectedTerminal_PropertyChanged;
                    }

                    selectedTerminal = value;

                    if (selectedTerminal != null)
                    {
                        selectedTerminal.PropertyChanged += SelectedTerminal_PropertyChanged;
                    }

                    UpdateModelState();
                }
            }
        }

        private void ResetSelectedTerminalUI()
        {
            Trace.WriteLine($"{nameof(ResetSelectedTerminalUI)}", GetType().FullName);
            TerminalStatusString = string.Empty;
            TerminalSettingsString = string.Empty;
            IsLoyaltyHandled = false;
        }

        private void SelectedTerminal_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TerminalDescriptorContext.SelectedConnectionId))
            {
                SelectTerminalCommand.RaiseCanExecuteChanged();
            }
        }

        public string TerminalSettingsString { get; private set; }

        public string TerminalStatusString { get; private set; }

        public string ToggleLoyaltyCommandTitle { get; set; } = AppStrings.LoyaltyActivate;

        private bool isLoyaltyActive;
        public bool IsLoyaltyActive
        {
            get => isLoyaltyActive;
            set
            {
                if (isLoyaltyActive != value)
                {
                    isLoyaltyActive = value;
                    OnPropertyChanged(nameof(IsLoyaltyActive));
                    ToggleLoyaltyCommandTitle = isLoyaltyActive ? AppStrings.LoyaltyDeactivate : AppStrings.LoyaltyActivate;
                    OnPropertyChanged(nameof(ToggleLoyaltyCommandTitle));
                }
            }
        }

        public OperationAccepted LastOpearationAccepted { get; private set; }

        private PromptDialog activePromptDialog;

        private string activePromptId;

        /// <summary>
        /// Gets the command for fetching terminals.
        /// </summary>
        public AppCommandBase GetTerminalsCommand { get; }
        public AppCommandBase SelectTerminalCommand { get; }
        public AppCommandBase GetTerminalSettingsCommand { get; }
        public AppCommandBase GetTerminalStatusCommand { get; }
        public AppCommandBase GetTerminalSessionCommand { get; }
        public AppCommandBase AbortTransactionCommand { get; }
        public AppCommandBase StartPurchaseCommand { get; }
        public AppCommandBase StartReversalCommand { get; }
        public AppCommandBase StartRefundCommand { get; }
        public AppCommandBase ToggleLoyaltyCommand { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        public MainWindow()
        {
            InitLogging();
            InitializeComponent();
            client = new PaymentTerminalServiceClient(new System.Net.Http.HttpClient());
            GetTerminalsCommand = new GetTerminalsCommandImpl(this);
            SelectTerminalCommand = new SelectTerminalCommandImpl(this);
            GetTerminalSettingsCommand = new GetTerminalSettingsCommandImpl(this);
            GetTerminalStatusCommand = new GetTerminalStatusCommandImpl(this);
            GetTerminalSessionCommand = new GetTerminalSessionCommandImpl(this);
            AbortTransactionCommand = new AbortTransactionCommandImpl(this);
            StartPurchaseCommand = new StartPurchaseCommandImpl(this);
            StartReversalCommand = new StartReversalCommandImpl(this);
            StartRefundCommand = new StartRefundCommandImpl(this);
            ToggleLoyaltyCommand = new ToggleLoyaltyCommandImpl(this);
            IsBusy = true;
            DataContext = this;
            Loaded += async (s, e) =>
            {
                await Task.Delay(1500);
                IsBusy = false;
            };
        }

        protected override void OnClosed(EventArgs e)
        {
            Trace.WriteLine($"{nameof(OnClosed)}", GetType().FullName);

            if (terminalStatusPoller != null)
            {
                terminalStatusPoller.StatusReceived -= TerminalStatusPoller_StatusReceived;
                terminalStatusPoller.Dispose();
                terminalStatusPoller = null;
            }

            if (TraceListener != null)
            {
                Trace.Listeners.Remove(TraceListener);
                TraceListener.Dispose();
                TraceListener = null;
            }

            base.OnClosed(e);
        }

        #region Logging

        private class ProgramTraceListener : TraceListener
        {
            private readonly Action<string> logAction;

            public ProgramTraceListener(Action<string> logAction)
            {
                this.logAction = logAction;
            }

            public override void Write(string message)
            {
                logAction($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
            }

            public override void WriteLine(string message)
            {
                logAction($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}");
            }
        }

        private void InitLogging()
        {
            TraceListener = new ProgramTraceListener(msg =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogMessages.Insert(0, msg);
                }));
            });
            Trace.Listeners.Add(TraceListener);
        }

        #endregion

        /// <summary>
        /// Raises the PropertyChanged event for the specified property.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Updates the model state and raises property change notifications for relevant properties.
        /// </summary>
        private void UpdateModelState()
        {
            OnPropertyChanged(nameof(HasTerminals));
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(TransactionAmount));
            OnPropertyChanged(nameof(TransactionId));
            OnPropertyChanged(nameof(TransactionTimestamp));
            OnPropertyChanged(nameof(ClientReference));
            OnPropertyChanged(nameof(SelectedTerminal));
            OnPropertyChanged(nameof(TerminalSettingsString));
            OnPropertyChanged(nameof(TerminalStatusString));
            OnPropertyChanged(nameof(IsLoyaltyHandled));
            OnPropertyChanged(nameof(IsLoyaltyAutoRepy));
            OnPropertyChanged(nameof(ToggleLoyaltyCommandTitle));
            OnPropertyChanged(nameof(IsLoyaltyActive));
            OnPropertyChanged(nameof(LastOpearationAccepted));
            OnPropertyChanged(nameof(TerminalStatusPollerIntervalSeconds));
            OnPropertyChanged(nameof(TerminalStatusPollerStartDelaySeconds));
            GetTerminalsCommand.RaiseCanExecuteChanged();
            SelectTerminalCommand.RaiseCanExecuteChanged();
            GetTerminalSettingsCommand.RaiseCanExecuteChanged();
            GetTerminalStatusCommand.RaiseCanExecuteChanged();
            GetTerminalSessionCommand.RaiseCanExecuteChanged();
            AbortTransactionCommand.RaiseCanExecuteChanged();
            StartPurchaseCommand.RaiseCanExecuteChanged();
            StartReversalCommand.RaiseCanExecuteChanged();
            StartRefundCommand.RaiseCanExecuteChanged();
            ToggleLoyaltyCommand.RaiseCanExecuteChanged();
        }

        private MessageBoxResult ShowMessage(string message, string caption, MessageBoxImage icon = MessageBoxImage.Information, MessageBoxButton buttons = MessageBoxButton.OK)
        {
            using (new MessageBoxCenterer(this))
            {
                return MessageBox.Show(this, message, caption, buttons, icon);
            }
        }

        private void TerminalStatusPoller_StatusReceived(object sender, TerminalStatusEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                var status = e.Status;

                TerminalStatusString = JsonConvert.SerializeObject(status, Formatting.Indented);

                if (status.State != TerminalState.Idle)
                {
                    Trace.WriteLine($"{nameof(TerminalStatusPoller_StatusReceived)}: state={status.State} operation={status.ActiveOperationType} final={status.LastResultIsFinal}", GetType().FullName);
                }

                if (status.LastResultIsFinal)
                {
                    terminalStatusPoller?.Stop();

                    // Backend moved past AwaitingPrompt (aborted or timed out) — close the dialog
                    // so HandlePromptIfNeededAsync unblocks and skips RespondToPrompt.
                    activePromptDialog?.CloseAborted();

                    // Automatically confirm the transaction when it completes
                    await ConfirmTransactionAsync(status);
                }

                bool loyaltyStillActive =
                    (status.State == TerminalState.WaitingForUserAction
                        || status.State == TerminalState.AwaitingResult)
                    && status.ActiveOperationType == OperationType.LoyaltyActivate;

                IsLoyaltyActive = loyaltyStillActive;

                if (status.State == TerminalState.AwaitingPrompt && status.Prompt != null)
                {
                    if (activePromptDialog != null
                        && activePromptId == status.Prompt.PromptId)
                    {
                        activePromptDialog.UpdatePrompt(status.Prompt);
                    }
                    else if (activePromptDialog == null)
                    {
                        await HandlePromptIfNeededAsync(status);
                    }
                    // else: different prompt arrived while dialog is open — ignore.
                }

                if (e.IsLastPoll)
                {
                    switch (e.StopReason)
                    {
                        case TerminalStatusPollStopReason.Stopped:
                            Trace.WriteLine("Terminal status poller stopped polling.", GetType().FullName);
                            break;

                        case TerminalStatusPollStopReason.MaxPollCountReached:
                            Trace.WriteLine("Terminal status poller stopped after reaching the configured max poll count.", GetType().FullName);
                            break;

                        case TerminalStatusPollStopReason.TimeoutReached:
                            Trace.WriteLine("Terminal status poller stopped after reaching the configured timeout.", GetType().FullName);
                            break;

                        case TerminalStatusPollStopReason.Faulted:
                            Trace.WriteLine($"Terminal status poller stopped due to error: {e.Error}", GetType().FullName);
                            break;

                        default:
                            if (e.Error != null)
                                Trace.WriteLine($"Terminal status poller stopped due to error: {e.Error}", GetType().FullName);
                            else
                                Trace.WriteLine("Terminal status poller finished.", GetType().FullName);
                            break;
                    }
                }

                UpdateModelState();
            }));
        }

        private async Task HandlePromptIfNeededAsync(TerminalStatus status)
        {
            Trace.WriteLine($"{nameof(HandlePromptIfNeededAsync)}: promptId={status?.Prompt?.PromptId}", GetType().FullName);

            var prompt = status?.Prompt;

            if (prompt == null)
                return;

            if (prompt.ExpiresAt != default && prompt.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                Trace.WriteLine($"{nameof(HandlePromptIfNeededAsync)}: prompt '{prompt.PromptId}' already expired, responding false.", GetType().FullName);
                await RespondToPromptAsync(prompt.PromptId, false, null);
                terminalStatusPoller?.Start();
                return;
            }

            activePromptId = prompt.PromptId;
            activePromptDialog = new PromptDialog(prompt) { Owner = this };

            // Keep polling while the dialog is open so the backend abort is detected.
            // Re-entrant dialogs are prevented by the activePromptDialog != null guard in the
            // status received handler. Use a slower interval to reduce noise during user interaction.
            terminalStatusPoller?.Start(TimeSpan.FromSeconds(terminalStatusPollerIntervalSeconds));

            bool accepted = activePromptDialog.ShowDialog() == true;
            bool isAborted = activePromptDialog.IsAborted;
            string input = (!prompt.YesNo && accepted) ? activePromptDialog.Input : null;

            activePromptDialog = null;
            activePromptId = null;

            if (isAborted)
            {
                // Backend already timed out and moved on — skip RespondToPrompt (would 409).
                // Poller was stopped by the final-status handler before CloseAborted() was called;
                // restart it now to pick up the final status.
                Trace.WriteLine($"{nameof(HandlePromptIfNeededAsync)}: prompt '{prompt.PromptId}' aborted by backend, skipping response.", GetType().FullName);
                terminalStatusPoller?.Start();
                return;
            }

            await RespondToPromptAsync(prompt.PromptId, accepted, input);
        }

        private async Task RespondToPromptAsync(string promptId, bool accepted, string input)
        {
            Trace.WriteLine($"{nameof(RespondToPromptAsync)}: promptId={promptId} accepted={accepted}", GetType().FullName);
            try
            {
                LastOpearationAccepted = await client.RespondToPromptAsync(new PromptResponseRequest
                {
                    PromptId = promptId,
                    YesNo = accepted,
                    Input = input,
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(RespondToPromptAsync)} failed: {ex}", GetType().FullName);
            }
        }

        private void CreateOrStartTerminalStatusPoller(
            int? maxPollCount = null,
            TimeSpan? timeout = null)
        {
            var interval = TimeSpan.FromSeconds(terminalStatusPollerIntervalSeconds);
            var startDelay = terminalStatusPollerStartDelaySeconds > 0
                ? TimeSpan.FromSeconds(terminalStatusPollerStartDelaySeconds)
                : (TimeSpan?)null;

            Trace.WriteLine($"{nameof(CreateOrStartTerminalStatusPoller)}: interval={interval} startDelay={startDelay} maxPollCount={maxPollCount} timeout={timeout}", GetType().FullName);

            if (terminalStatusPoller == null)
            {
                terminalStatusPoller = new TerminalStatusPoller(
                    client.GetSelectedTerminalStatusAsync,
                    interval,
                    maxPollCount,
                    timeout);

                terminalStatusPoller.StatusReceived += TerminalStatusPoller_StatusReceived;
            }
            else
            {
                terminalStatusPoller.Configure(interval, maxPollCount, timeout);
            }

            terminalStatusPoller.Start(startDelay);
        }

        /// <summary>
        /// Loads all available terminals from the backend service and populates the UI collection.
        /// </summary>
        private async Task LoadTerminalsAsync()
        {
            Trace.WriteLine($"{nameof(LoadTerminalsAsync)}", GetType().FullName);
            IsBusy = true;
            ResetSelectedTerminalUI();
            try
            {
                if (paymentTerminals.Count > 0)
                {
                    paymentTerminals.Clear();
                }

                TerminalCatalogResponse terminals = await client.GetTerminalsAsync();

                if (terminals != null)
                {
                    foreach (TerminalDescriptor terminal in terminals.Terminals)
                    {
                        DataFactory.ToContext(terminal, out TerminalDescriptorContext item);
                        paymentTerminals.Add(item);
                    }

                    //update selected terminal and connection based on backend response
                    SelectedTerminal = paymentTerminals.FirstOrDefault(t => t.TerminalId == terminals.SelectedTerminalId);
                }
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private async Task SelectTerminalAsync()
        {
            Trace.WriteLine($"{nameof(SelectTerminalAsync)}", GetType().FullName);

            IsBusy = true;

            bool isSelectionFailed = false;

            try
            {
                DataFactory.ToDto(SelectedTerminal.SelectedConnectionContext, out TerminalConnectionOption connection);
                var request = new SelectTerminalRequest
                {
                    TerminalId = SelectedTerminal.TerminalId,
                    Connection = connection,
                    IsLoyaltySupported = SelectedTerminal.IsLoyaltySupported,
                    VendorPayload = SelectedTerminal.VendorPayload
                };

                SelectedTerminalResponse response = await client.SelectTerminalAsync(request);

                //update selected terminal and connection based on backend response
                SelectedTerminal = paymentTerminals.FirstOrDefault(t => t.TerminalId == response?.Selected?.TerminalId);
            }
            catch
            {
                isSelectionFailed = true;
                throw;
            }
            finally
            {
                IsBusy = false;

                if (isSelectionFailed)
                {
                    await LoadTerminalsAsync(); //refresh terminal list to reflect actual state after failed selection attempt
                }
                else
                {
                    UpdateModelState();
                }
            }
        }

        private async Task GetTerminalSettingsAsync()
        {
            Trace.WriteLine($"{nameof(GetTerminalSettingsAsync)}", GetType().FullName);
            isBusy = true;

            try
            {
                var settings = await client.GetSelectedTerminalSettingsAsync();
                if (settings != null)
                {
                    TerminalSettingsString = JsonConvert.SerializeObject(settings, Formatting.Indented);
                }
            }
            finally
            {
                isBusy = false;
                UpdateModelState();
            }
        }

        private async Task GetTerminalStatusAsync()
        {
            Trace.WriteLine($"{nameof(GetTerminalStatusAsync)}", GetType().FullName);
            IsBusy = true;
            try
            {
                var status = await client.GetSelectedTerminalStatusAsync();
                if (status != null)
                {
                    TerminalStatusString = JsonConvert.SerializeObject(status, Formatting.Indented);
                }
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private async Task GetTerminalSessionAsync()
        {
            Trace.WriteLine($"{nameof(GetTerminalSessionAsync)}", GetType().FullName);
            IsBusy = true;
            try
            {
                var session = await client.GetSelectedTerminalSessionAsync();
                if (session != null)
                {
                    TerminalStatusString = JsonConvert.SerializeObject(session, Formatting.Indented);
                }
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private async Task AbortTransactionAsync()
        {
            Trace.WriteLine($"{nameof(AbortTransactionAsync)}", GetType().FullName);
            IsBusy = true;
            try
            {
                terminalStatusPoller?.Stop();
                LastOpearationAccepted = await client.AbortTransactionAsync();
                CreateOrStartTerminalStatusPoller();
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private async Task ConfirmTransactionAsync(TerminalStatus status)
        {
            Trace.WriteLine($"{nameof(ConfirmTransactionAsync)}: sessionName={status?.SessionName}", GetType().FullName);

            try
            {
                var request = new TransactionConfirmRequest();

                // Include session name in VendorPayload if available
                if (!string.IsNullOrWhiteSpace(status.SessionName))
                {
                    request.VendorPayload = new VendorPayload
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { PaymentTerminalBase.SessionNamePropertyName, status.SessionName }
                        }
                    };
                }

                LastOpearationAccepted = await client.ConfirmTransactionAsync(request);
                Trace.WriteLine($"{nameof(ConfirmTransactionAsync)}: confirmation successful for session '{status.SessionName}'.", GetType().FullName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(ConfirmTransactionAsync)} failed: {ex}", GetType().FullName);
            }
        }

        private async Task StartPurchaseAsync()
        {
            Trace.WriteLine($"{nameof(StartPurchaseAsync)}", GetType().FullName);
            IsBusy = true;
            try
            {
                terminalStatusPoller?.Stop();

                var request = new PurchaseRequest
                {
                    Amount = (long)(TransactionAmount * 100),
                    Currency = "EUR",
                    ClientReference = ClientReference,
                    IsLoyaltyHandled = IsLoyaltyHandled
                };
                LastOpearationAccepted = await client.StartPurchaseAsync(request);
                CreateOrStartTerminalStatusPoller();
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private async Task StartReversalAsync()
        {
            Trace.WriteLine($"{nameof(StartReversalAsync)}", GetType().FullName);
            IsBusy = true;
            try
            {
                terminalStatusPoller?.Stop();

                var request = new ReversalRequest
                {
                    TransactionId = TransactionId,
                    Timestamp = TransactionTimestamp,
                    ClientReference = ClientReference,
                };
                LastOpearationAccepted = await client.StartReversalAsync(request);
                CreateOrStartTerminalStatusPoller();
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private async Task StartRefundAsync()
        {
            Trace.WriteLine($"{nameof(StartRefundAsync)}", GetType().FullName);
            IsBusy = true;
            try
            {
                terminalStatusPoller?.Stop();

                var request = new RefundRequest
                {
                    Amount = (long)(TransactionAmount * 100),
                    Currency = "EUR",
                    ClientReference = ClientReference,
                };
                LastOpearationAccepted = await client.StartRefundAsync(request);
                CreateOrStartTerminalStatusPoller();
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private async Task ToggleLoyaltyAsync()
        {
            Trace.WriteLine($"{nameof(ToggleLoyaltyAsync)}", GetType().FullName);
            IsBusy = true;
            try
            {
                if (IsLoyaltyActive)
                {
                    terminalStatusPoller?.Stop();
                    LastOpearationAccepted = await client.LoyaltyDeactivateAsync(new BaseActionRequest());
                    IsLoyaltyActive = false;
                    CreateOrStartTerminalStatusPoller();
                }
                else
                {
                    LastOpearationAccepted = await client.LoyaltyActivateAsync(
                        new LoyaltyActivateRequest
                        {
                            ClientReference = ClientReference,
                            Mode = IsLoyaltyAutoRepy
                                ? LoyaltyActivateRequestMode.Autoreply
                                : LoyaltyActivateRequestMode.Default
                        });
                    IsLoyaltyActive = true;
                    CreateOrStartTerminalStatusPoller();
                }
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private int terminalStatusPollerIntervalSeconds = 3;
        /// <summary>
        /// Gets or sets the polling interval in seconds used when starting the terminal status poller.
        /// </summary>
        public int TerminalStatusPollerIntervalSeconds
        {
            get => terminalStatusPollerIntervalSeconds;
            set
            {
                if (terminalStatusPollerIntervalSeconds != value)
                {
                    terminalStatusPollerIntervalSeconds = value;
                    UpdateModelState();
                }
            }
        }

        private int terminalStatusPollerStartDelaySeconds = 1;
        /// <summary>
        /// Gets or sets the initial delay in seconds before the first poll fires after the poller is started.
        /// </summary>
        public int TerminalStatusPollerStartDelaySeconds
        {
            get => terminalStatusPollerStartDelaySeconds;
            set
            {
                if (terminalStatusPollerStartDelaySeconds != value)
                {
                    terminalStatusPollerStartDelaySeconds = value;
                    UpdateModelState();
                }
            }
        }
    }
}
