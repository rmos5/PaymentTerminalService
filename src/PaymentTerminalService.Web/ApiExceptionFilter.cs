using System;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;
using PaymentTerminalService.Model;

namespace PaymentTerminalService.Web
{
    /// <summary>
    /// Global exception filter for consistent API error responses.
    /// Handles exceptions thrown by API controllers and converts them to structured error responses
    /// with appropriate HTTP status codes and error payloads.
    /// </summary>
    public class ApiExceptionFilter : ExceptionFilterAttribute
    {
        /// <summary>
        /// Called when an unhandled exception occurs in an API controller action.
        /// Maps known exception types to specific HTTP status codes and error codes,
        /// and returns a structured <see cref="ErrorResponse"/> to the client.
        /// </summary>
        /// <param name="context">The context for the exception, including request and exception details.</param>
        public override void OnException(HttpActionExecutedContext context)
        {
            HttpStatusCode status;
            string code;
            string details = null;
#if DEBUG
            details = context.Exception.StackTrace; // Include stack trace for debugging purposes
#endif

            // Map custom API exceptions to specific HTTP status codes
            if (context.Exception is ApiBadRequestException)
            {
                status = HttpStatusCode.BadRequest;
                code = status.ToString();
            }
            else if (context.Exception is ApiNotFoundException)
            {
                status = HttpStatusCode.NotFound;
                code = status.ToString();
            }
            else if (context.Exception is ApiConflictException)
            {
                status = HttpStatusCode.Conflict;
                code = status.ToString();
            }
            else
            {
                // Default to InternalServerError for unknown exceptions
                status = HttpStatusCode.InternalServerError;
                code = status.ToString();
            }

            string message = context.Exception.Message;
            if (context.Exception.InnerException != null)
                message = $"{message}\n{context.Exception.InnerException.Message}";

            // Build structured error response
            var error = new ErrorResponse
            {
                Code = code,
                Message = message,
                Details = details
            };

            // Set the HTTP response with the error payload
            context.Response = context.Request.CreateResponse(status, error);
        }
    }
}