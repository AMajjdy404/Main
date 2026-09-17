namespace Main.API.Errors
{
    public class ApiErrorResponse : ApiResponse
    {
        public string? Details { get; set; }

        public ApiErrorResponse(int statusCode, string? message = null, string? details = null)
            : base(statusCode, message)
        {
            Details = details;
        }
    }
}
