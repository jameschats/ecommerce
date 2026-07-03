using ecomm.api.Common.Exceptions;
using ecomm.api.Common.Models;

namespace ecomm.api.Common.Middleware;

/// <summary>Converts AppException into a clean ApiResponse; logs anything else as 500.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            var resp = ApiResponse<object>.Fail(ex.Message);
            resp.CorrelationId = Cid(context);
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(resp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Path}", context.Request.Path);
            var resp = ApiResponse<object>.Fail("An unexpected error occurred.");
            resp.CorrelationId = Cid(context);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(resp);
        }
    }

    private static string? Cid(HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var v) ? v as string : null;
}
