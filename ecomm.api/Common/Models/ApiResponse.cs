namespace ecomm.api.Common.Models;

/// <summary>
/// Standard envelope for API responses, so the Angular client sees a
/// consistent shape for success and failure.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<string>? Errors { get; init; }
    /// <summary>Request trace id — set on error responses so it can be quoted in a support ticket. See CorrelationIdMiddleware.</summary>
    public string? CorrelationId { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}
