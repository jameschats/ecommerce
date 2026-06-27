namespace ecomm.api.Common.Exceptions;

/// <summary>
/// A handled application error mapped to an HTTP status by the
/// exception-handling middleware (defaults to 400 Bad Request).
/// </summary>
public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = StatusCodes.Status400BadRequest)
        : base(message) => StatusCode = statusCode;
}
