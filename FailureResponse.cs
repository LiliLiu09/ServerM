namespace Server.Management
{
    public class FailureResponse<T> : Response<T>
    {
        public FailureResponse(string message, string? errorDetails = null, Exception? exception = null)
        {
            Success = false;
            Message = message;
            ErrorDetails = errorDetails;
            Exception = exception;
            Result = default;
        }
    }
}
