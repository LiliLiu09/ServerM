using ServerManager;
using ServerManager.Interfaces;

namespace Server.Management
{
    /// <summary>
    /// Represents a standardized response.
    /// </summary>
    /// <typeparam name="T">The type of the data included in the response.</typeparam>
    public class Response<T> : IResponse
    {
        public static Response<T> CreateResponse( T returnValue, string message, bool success = true, string? errorDetails = null, Exception exception = null)
        {
            if (success)
            {
                return new SuccessResponse<T>(returnValue, message);
            }

            return new FailureResponse<T>( message, errorDetails , exception);
        }
        /// <summary>
        /// Gets or sets a value which indicates whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets a message providing additional information about the response.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Gets or sets the result returned by the operation.
        /// </summary>
        public T? Result { get; set; }

        /// <summary>
        /// Gets or sets additional error information if the operation was not successful.
        /// </summary>
        public string? ErrorDetails { get; set; }

        /// <summary>
        /// Gets or sets the exception object associated with the operation failure, useful for internal logging and diagnostics.
        /// </summary>
        public Exception? Exception { get; set; }
    }
}
