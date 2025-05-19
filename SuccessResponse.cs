namespace Server.Management
{
    public class SuccessResponse<T> : Response<T>
    {
        public SuccessResponse(T result, string? message = null)
        {
            Success = true;
            Message = message ?? "Operation succeeded.";
            Result = result;
        }
    }
}
