using System;

namespace PaymentTerminalService.Model
{
    /// <summary>
    /// Exception representing a 400 Bad Request error.
    /// Throw this when the client sends invalid input or the request cannot be processed.
    /// </summary>
    public class ApiBadRequestException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiBadRequestException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public ApiBadRequestException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiBadRequestException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ApiBadRequestException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception representing a 404 Not Found error.
    /// Throw this when a requested resource does not exist.
    /// </summary>
    public class ApiNotFoundException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiNotFoundException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public ApiNotFoundException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiNotFoundException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ApiNotFoundException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception representing a 409 Conflict error.
    /// Throw this when a request cannot be completed due to a conflict with the current state of the resource.
    /// </summary>
    public class ApiConflictException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ApiConflictException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        public ApiConflictException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiConflictException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ApiConflictException(string message, Exception innerException) : base(message, innerException) { }
    }
}