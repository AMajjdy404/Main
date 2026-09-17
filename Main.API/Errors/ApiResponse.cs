namespace Main.API.Errors
{
    public class ApiResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; }
        public object? Data { get; set; }

        public ApiResponse(int statusCode, string? message = null, object? data = null)
        {
            StatusCode = statusCode;
            Message = message ?? GetDefaultMessageForStatusCode(statusCode);
            Data = data;
        }

        private static string GetDefaultMessageForStatusCode(int statusCode)
        {
            return statusCode switch
            {
                200 => "Success",
                201 => "Created successfully",
                400 => "Bad request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Resource not found",
                409 => "Conflict",
                500 => "Internal server error",
                _ => "Request completed"
            };
        }
    }
}
