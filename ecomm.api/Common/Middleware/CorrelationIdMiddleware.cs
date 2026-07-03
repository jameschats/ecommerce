namespace ecomm.api.Common.Middleware;

/// <summary>
/// Assigns a correlation id to every request (honouring an inbound X-Correlation-Id),
/// stashes it on HttpContext.Items + the response header, so an error a merchant sees
/// can be traced through the logs and quoted in a support ticket (design-v2.md V2-10).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    public async Task Invoke(HttpContext context)
    {
        var id = context.Request.Headers.TryGetValue(HeaderName, out var incoming) && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("n");

        context.Items[ItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        await next(context);
    }
}
