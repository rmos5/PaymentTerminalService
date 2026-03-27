using Newtonsoft.Json;
using PaymentTerminalService.Client;
using PaymentTerminalService.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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

        private abstract class TransactionCommandBase : SelectedTerminalCommandBase
        {
            protected TransactionCommandBase(MainWindow context) : base(context) { }

            public override bool CanExecute(object parameter)
            {
                return base.CanExecute(parameter)
                    && !Context.IsTransactionActive;
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
                bool force = parameter is bool b && b;
                await Context.AbortTransactionAsync(force);
            }
        }

        private class StartPurchaseCommandImpl : TransactionCommandBase
        {
            public StartPurchaseCommandImpl(MainWindow context) : base(context) { }

            public override bool CanExecute(object parameter)
            {
                // Allow purchase when loyalty is active — loyalty activation is a
                // prerequisite step that leads directly into a purchase.
                if (Context.IsLoyaltyActive)
                    return !Context.IsBusy
                        && Context.SelectedTerminal?.SelectedConnectionId != null;

                return base.CanExecute(parameter);
            }

            public override async void Execute(object parameter)
            {
                await Context.StartPurchaseAsync();
            }
        }

        private class StartReversalCommandImpl : TransactionCommandBase
        {
            public StartReversalCommandImpl(MainWindow context) : base(context) { }

            public override async void Execute(object parameter)
            {
                await Context.StartReversalAsync();
            }
        }

        private class StartRefundCommandImpl : TransactionCommandBase
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

            public override bool CanExecute(object parameter)
            {
                // Enable when:
                // - Terminal is selected AND connection chosen
                // - Not busy
                // - Either no transaction active, OR loyalty is active (to allow deactivation)
                return base.CanExecute(parameter)
                    && (!Context.IsTransactionActive || Context.IsLoyaltyActive);
            }

            public override async void Execute(object parameter)
            {
                await Context.ToggleLoyaltyAsync();
            }
        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private PaymentTerminalServiceManager terminalServiceManager;

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

        private DateTime transactionTimestamp;
        /// <summary>
        /// Gets or sets the transaction timestamp for the current transaction.
        /// </summary>
        public DateTime TransactionTimestamp
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

        private bool isHandlingFinalStatus = false;

        private bool isTransactionActive;
        /// <summary>
        /// Gets a value indicating whether a transaction terminalServiceManager is currently active on the terminal.
        /// While true, new transaction commands (Purchase, Refund, Reversal, Loyalty) are disabled.
        /// </summary>
        public bool IsTransactionActive
        {
            get => isTransactionActive;
            private set
            {
                if (isTransactionActive != value)
                {
                    isTransactionActive = value;
                    UpdateModelState();
                }
            }
        }

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

            terminalServiceManager = new PaymentTerminalServiceManager(ConfigurationManager.AppSettings["PaymentTerminalServiceUrl"]);

            terminalServiceManager.StatusReceived += TerminalServiceManager_StatusReceived;

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

            if (terminalServiceManager != null)
            {
                terminalServiceManager.StatusReceived -= TerminalServiceManager_StatusReceived;
                terminalServiceManager.Dispose();
                terminalServiceManager = null;
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
            OnPropertyChanged(nameof(IsTransactionActive));
            OnPropertyChanged(nameof(LastOpearationAccepted));
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

        private void TerminalServiceManager_StatusReceived(object sender, TerminalStatusEventArgs e)
        {
            HandleTerminalStatus(e);
        }


        private T GetValueOrDefault<T>(IDictionary<string, object> payload, string key, T defaultValue)
        {
            if (payload == null || !payload.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            if (value is T typed)
                return typed;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(GetValueOrDefault)}: failed to convert key '{key}' value '{value}' to {typeof(T).Name}: {ex.Message}", GetType().FullName);
                return defaultValue;
            }
        }

        private void HandleTerminalStatus(TerminalStatusEventArgs e)
        {
            Trace.WriteLine($"{nameof(HandleTerminalStatus)}:{e}", GetType().FullName);

            if (e.Status == null)
            {
                Trace.WriteLine($"{nameof(HandleTerminalStatus)}: received null status, ignoring.", GetType().FullName);
                return;
            }

            Dispatcher.BeginInvoke(new Action(async () =>
            {
                var status = e.Status;

                TerminalStatusString = JsonConvert.SerializeObject(status, Formatting.Indented);

                if (status.LastResultIsFinal && !isHandlingFinalStatus)
                {
                    isHandlingFinalStatus = true;

                    // Backend moved past AwaitingPrompt (aborted or timed out) — close the dialog
                    // so HandlePromptIfNeededAsync unblocks and skips RespondToPrompt.
                    activePromptDialog?.CloseAborted();

                    bool isTransactionFinal = !string.IsNullOrWhiteSpace(status.SessionName)
                        && (status.State == TerminalState.Completed || status.State == TerminalState.Aborted);

                    try
                    {
                        if (isTransactionFinal)
                        {
                            IDictionary<string, object> payload = status.OperationResult?.VendorPayload?.AdditionalProperties ?? new Dictionary<string, object>();
                            
                            TransactionId = GetValueOrDefault(payload, VendorPayloadKeys.TransactionId, string.Empty);
                            TransactionTimestamp = GetValueOrDefault<DateTime>(payload, VendorPayloadKeys.TransactionDateTime, default);
                            await ConfirmTransactionAsync(status);
                        }
                    }
                    finally
                    {
                        isHandlingFinalStatus = false;
                    }
                }

                bool loyaltyStillActive =
                    (status.State == TerminalState.WaitingForUserAction
                        || status.State == TerminalState.AwaitingResult)
                    && status.ActiveOperationType == OperationType.LoyaltyActivate;

                IsLoyaltyActive = loyaltyStillActive;

                // Block new transaction commands while the terminal reports an active terminalServiceManager.
                // Cleared when the status becomes final or returns to Idle.
                IsTransactionActive = !status.LastResultIsFinal
                    && status.State != TerminalState.Idle
                    && status.State != TerminalState.Error;

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
                return;
            }

            activePromptId = prompt.PromptId;
            activePromptDialog = new PromptDialog(prompt) { Owner = this };

            bool accepted = activePromptDialog.ShowDialog() == true;
            bool isAborted = activePromptDialog.IsAborted;
            string input = (!prompt.YesNo && accepted) ? activePromptDialog.Input : null;

            activePromptDialog = null;
            activePromptId = null;

            if (isAborted)
            {
                // Backend already timed out and moved on — skip RespondToPrompt (would 409).
                // Session will continue polling and deliver the final status.
                Trace.WriteLine($"{nameof(HandlePromptIfNeededAsync)}: prompt '{prompt.PromptId}' aborted by backend, skipping response.", GetType().FullName);
                return;
            }

            await RespondToPromptAsync(prompt.PromptId, accepted, input);
        }

        private async Task RespondToPromptAsync(string promptId, bool accepted, string input)
        {
            Trace.WriteLine($"{nameof(RespondToPromptAsync)}: promptId={promptId} accepted={accepted}", GetType().FullName);
            try
            {
                LastOpearationAccepted = await terminalServiceManager.RespondToPromptAsync(new PromptResponseRequest
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

                TerminalCatalogResponse terminals = await terminalServiceManager.GetTerminalsAsync();

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

                SelectedTerminalResponse response = await terminalServiceManager.SelectTerminalAsync(request);

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
                var settings = await terminalServiceManager.GetSelectedTerminalSettingsAsync();
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
                var status = await terminalServiceManager.GetSelectedTerminalStatusAsync();
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
                var sessionData = await terminalServiceManager.GetSelectedTerminalSessionAsync();
                if (sessionData != null)
                {
                    TerminalStatusString = JsonConvert.SerializeObject(sessionData, Formatting.Indented);
                }
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private async Task AbortTransactionAsync(bool force)
        {
            Trace.WriteLine($"{nameof(AbortTransactionAsync)}:{force}", GetType().FullName);
            IsBusy = true;
            try
            {
                var request = new AbortTransactionRequest
                {
                    Force = force
                };
                LastOpearationAccepted = await terminalServiceManager.AbortTransactionAsync(request);
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }

        private async Task ConfirmTransactionAsync(TerminalStatus status)
        {
            Trace.WriteLine($"{nameof(ConfirmTransactionAsync)}: sessionName={status.SessionName}", GetType().FullName);
            try
            {
                var request = new TransactionConfirmRequest
                {
                    VendorPayload = new VendorPayload
                    {
                        AdditionalProperties = new Dictionary<string, object>
                        {
                            { VendorPayloadKeys.SessionName, status.SessionName }
                        }
                    }
                };

                LastOpearationAccepted = await terminalServiceManager?.ConfirmTransactionAsync(request);
                Trace.WriteLine($"{nameof(ConfirmTransactionAsync)}: confirmation successful for terminalServiceManager '{status.SessionName}'.", GetType().FullName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{nameof(ConfirmTransactionAsync)}\n{ex}", GetType().FullName);
            }
        }

        private static readonly string[] cashierIds = { "EMIL", "BOB", "RAND", "ANNA", "LEE" };

        private int clientReferenceSequence = 0;

        private string GenerateClientReference()
        {
            int sequence = ++clientReferenceSequence;
            string cashier = cashierIds[(sequence - 1) % cashierIds.Length];
            return $"{sequence}/{cashier}/{DateTime.Now:yyyyMMdd}";
        }

        private async Task StartPurchaseAsync()
        {
            Trace.WriteLine($"{nameof(StartPurchaseAsync)}", GetType().FullName);
            IsBusy = true;
            IsTransactionActive = true;
            try
            {
                ClientReference = GenerateClientReference();
                var request = new PurchaseRequest
                {
                    Amount = (long)(TransactionAmount * 100),
                    Currency = "EUR",
                    ClientReference = ClientReference,
                    IsLoyaltyHandled = IsLoyaltyHandled
                };
                LastOpearationAccepted = await terminalServiceManager.StartPurchaseAsync(request);
            }
            catch
            {
                // Operation did not start — lift the optimistic lock immediately
                // so the buttons re-enable without waiting for the next poll.
                IsTransactionActive = false;
                throw;
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
            IsTransactionActive = true;
            try
            {
                var request = new ReversalRequest
                {
                    TransactionId = TransactionId,
                    Timestamp = TransactionTimestamp,
                    ClientReference = ClientReference,
                };
                LastOpearationAccepted = await terminalServiceManager.StartReversalAsync(request);
            }
            catch
            {
                IsTransactionActive = false;
                throw;
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
            IsTransactionActive = true;
            try
            {
                ClientReference = GenerateClientReference();

                var request = new RefundRequest
                {
                    Amount = (long)(TransactionAmount * 100),
                    Currency = "EUR",
                    ClientReference = ClientReference,
                };
                LastOpearationAccepted = await terminalServiceManager.StartRefundAsync(request);
            }
            catch
            {
                IsTransactionActive = false;
                throw;
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
            IsTransactionActive = true;
            try
            {
                if (IsLoyaltyActive)
                {
                    LastOpearationAccepted = await terminalServiceManager.LoyaltyDeactivateAsync(new BaseActionRequest());
                    IsLoyaltyActive = false;
                }
                else
                {
                    LastOpearationAccepted = await terminalServiceManager.LoyaltyActivateAsync(
                        new LoyaltyActivateRequest
                        {
                            ClientReference = ClientReference,
                            Mode = IsLoyaltyAutoRepy
                                ? LoyaltyActivateRequestMode.Autoreply
                                : LoyaltyActivateRequestMode.Default
                        });
                    IsLoyaltyActive = true;
                }
            }
            catch
            {
                IsTransactionActive = false;
                throw;
            }
            finally
            {
                IsBusy = false;
                UpdateModelState();
            }
        }
    }
}
