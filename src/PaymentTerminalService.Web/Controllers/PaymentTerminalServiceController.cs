using PaymentTerminalService.Model;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;

namespace PaymentTerminalService.Web.Controllers
{
    /// <inheritdoc />
    public partial class PaymentTerminalServiceController : PaymentTerminalServiceControllerBase
    {
        private readonly IPaymentTerminalSelector _selectorService;
        private IPaymentTerminal _selectedTerminal => _selectorService.SelectedPaymentTerminal;

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentTerminalServiceController"/> class.
        /// </summary>
        /// <param name="selectorService">
        /// The service responsible for terminal discovery, selection, and activation logic.
        /// This abstraction enables the controller to interact with the payment terminal catalog and selection state
        /// without depending on specific implementation details.
        /// </param>
        public PaymentTerminalServiceController(IPaymentTerminalSelector selectorService)
        {
            _selectorService = selectorService ?? throw new ArgumentNullException(nameof(selectorService));
        }

        /// <inheritdoc />
        [HttpGet, Route("terminals")]
        [ResponseType(typeof(TerminalCatalogResponse))]
        public override async Task<TerminalCatalogResponse> GetTerminals()
        {
            Trace.WriteLine($"{nameof(GetTerminals)}", GetType().FullName);
            var result = await _selectorService.GetTerminalsAsync();
            if (result == null)
            {
                throw new ApiNotFoundException("No terminals found.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpGet, Route("terminals/selected")]
        [ResponseType(typeof(SelectedTerminalResponse))]
        public override async Task<SelectedTerminalResponse> GetSelectedTerminal()
        {
            Trace.WriteLine($"{nameof(GetSelectedTerminal)}", GetType().FullName);
            var result = await _selectorService.GetSelectedTerminalAsync();
            if (result == null)
            {
                throw new ApiNotFoundException("No selected terminal found.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpPut, Route("terminals/selected")]
        [ResponseType(typeof(SelectedTerminalResponse))]
        public override async Task<SelectedTerminalResponse> SelectTerminal([FromBody] SelectTerminalRequest request)
        {
            Trace.WriteLine($"{nameof(SelectTerminal)}:{request}", GetType().FullName);
            if (request == null)
            {
                Trace.WriteLine("SelectTerminal: request is null", GetType().FullName);
                throw new ApiBadRequestException("Request is null.");
            }
            
            var result = await _selectorService.SelectTerminalAsync(request);
            if (result == null)
            {
                throw new ApiNotFoundException("Terminal selection failed.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpGet, Route("terminals/selected/settings")]
        [ResponseType(typeof(TerminalSettings))]
        public async override Task<TerminalSettings> GetSelectedTerminalSettings()
        {
            Trace.WriteLine($"{nameof(GetSelectedTerminalSettings)}", GetType().FullName);
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            var result = await _selectedTerminal.GetTerminalSettingsAsync();
            if (result == null)
            {
                throw new ApiNotFoundException("Terminal settings not found.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpGet, Route("terminals/selected/status")]
        [ResponseType(typeof(TerminalStatus))]
        public async override Task<TerminalStatus> GetSelectedTerminalStatus()
        {
            Trace.WriteLine($"{nameof(GetSelectedTerminalStatus)}", GetType().FullName);
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            var result = await _selectedTerminal.GetTerminalStatusAsync();
            if (result == null)
            {
                throw new ApiNotFoundException("Terminal status not found.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpGet, Route("terminals/selected/session")]
        [ResponseType(typeof(TerminalSessionResponse))]
        public async override Task<TerminalSessionResponse> GetSelectedTerminalSession()
        {
            Trace.WriteLine($"{nameof(GetSelectedTerminalSession)}", GetType().FullName);
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            var result = await _selectedTerminal.GetTerminalSessionAsync();
            if (result == null)
            {
                throw new ApiNotFoundException("Terminal session not found.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpPost, Route("terminals/selected/purchase")]
        [ResponseType(typeof(OperationAccepted))]
        public override async Task<OperationAccepted> StartPurchase([FromBody] PurchaseRequest request)
        {
            Trace.WriteLine($"{nameof(StartPurchase)}:{request}", GetType().FullName);
            if (request == null)
            {
                Trace.WriteLine("StartPurchase: request is null", GetType().FullName);
                throw new ApiBadRequestException("Request is null.");
            }
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            
            var result = await _selectedTerminal.StartPurchaseAsync(request);
            if (result == null)
            {
                throw new ApiNotFoundException("Couldn't start purchase.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpPost, Route("terminals/selected/refund")]
        [ResponseType(typeof(OperationAccepted))]
        public override async Task<OperationAccepted> StartRefund([FromBody] RefundRequest request)
        {
            Trace.WriteLine($"{nameof(StartRefund)}:{request}", GetType().FullName);
            if (request == null)
            {
                throw new ApiBadRequestException("Request is null.");
            }
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            
            var result = await _selectedTerminal.StartRefundAsync(request);
            if (result == null)
            {
                throw new ApiNotFoundException("Couldn't start refund.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpPost, Route("terminals/selected/reversal")]
        [ResponseType(typeof(OperationAccepted))]
        public override async Task<OperationAccepted> StartReversal([FromBody] ReversalRequest request)
        {
            Trace.WriteLine($"{nameof(StartReversal)}:{request}", GetType().FullName);
            if (request == null)
            {
                throw new ApiBadRequestException("Request is null.");
            }
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            
            var result = await _selectedTerminal.StartReversalAsync(request);
            if (result == null)
            {
                throw new ApiNotFoundException("Couldn't start reversal.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpPost, Route("terminals/selected/abort")]
        [ResponseType(typeof(OperationAccepted))]
        public override async Task<OperationAccepted> AbortTransaction([FromBody] AbortTransactionRequest request)
        {
            Trace.WriteLine($"{nameof(AbortTransaction)}", GetType().FullName);
            if (request == null)
            {
                throw new ApiBadRequestException("Request is null.");
            }
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            var result = await _selectedTerminal.AbortTransactionAsync(request);
            if (result == null)
            {
                throw new ApiNotFoundException("Abort transaction failed.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpPost, Route("terminals/selected/loyalty/activate")]
        [ResponseType(typeof(OperationAccepted))]
        public override async Task<OperationAccepted> LoyaltyActivate([FromBody] LoyaltyActivateRequest request)
        {
            Trace.WriteLine($"{nameof(LoyaltyActivate)}:{request}", GetType().FullName);
            if (request == null)
            {
                throw new ApiBadRequestException("Request is null.");
            }
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            var result = await _selectedTerminal.LoyaltyActivateAsync(request);
            if (result == null)
            {
                throw new ApiNotFoundException("Couldn't activate loyalty.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpPost, Route("terminals/selected/loyalty/deactivate")]
        [ResponseType(typeof(OperationAccepted))]
        public override async Task<OperationAccepted> LoyaltyDeactivate([FromBody] BaseActionRequest request)
        {
            Trace.WriteLine($"{nameof(LoyaltyDeactivate)}:{request}", GetType().FullName);
            if (request == null)
            {
                throw new ApiBadRequestException("Request is null.");
            }
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            
            var result = await _selectedTerminal.LoyaltyDeactivateAsync(request);
            if (result == null)
            {
                throw new ApiNotFoundException("Couldn't deactivate loyalty.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpPost, Route("terminals/selected/prompt")]
        [ResponseType(typeof(OperationAccepted))]
        public override async Task<OperationAccepted> RespondToPrompt([FromBody] PromptResponseRequest request)
        {
            Trace.WriteLine($"{nameof(RespondToPrompt)}:{request}", GetType().FullName);
            if (request == null)
            {
                throw new ApiBadRequestException("Request is null.");
            }
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            var result = await _selectedTerminal.RespondToPromptAsync(request);
            if (result == null)
            {
                throw new ApiNotFoundException("Couldn't respond to prompt.");
            }
            return result;
        }

        /// <inheritdoc />
        [HttpPost, Route("terminals/selected/confirm")]
        [ResponseType(typeof(OperationAccepted))]
        public override async Task<OperationAccepted> ConfirmTransaction([FromBody] TransactionConfirmRequest request)
        {
            Trace.WriteLine($"{nameof(ConfirmTransaction)}:{request}", GetType().FullName);
            if (request == null)
            {
                throw new ApiBadRequestException("Request is null.");
            }
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }

            var result = await _selectedTerminal.ConfirmTransactionAsync(request);
            if (result == null)
            {
                throw new ApiNotFoundException("Couldn't confirm transaction.");
            }

            return result;
        }

        /// <inheritdoc />
        [HttpDelete, Route("terminals/selected")]
        [ResponseType(typeof(OperationAccepted))]
        public override async Task<OperationAccepted> DeselectTerminal()
        {
            Trace.WriteLine($"{nameof(DeselectTerminal)}", GetType().FullName);
            if (_selectedTerminal == null)
            {
                throw new ApiNotFoundException("No selected terminal.");
            }
            
            var result = await _selectorService.DeselectTerminalAsync();
            if (result == null)
            {
                throw new ApiNotFoundException("Deselection failed.");
            }
            return result;
        }
    }
}