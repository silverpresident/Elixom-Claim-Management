namespace ElixomClaim.Web.Middleware;

public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = GetOrGenerateCorrelationId(context);

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
            {
                context.Response.Headers.Append(HeaderName, correlationId);
            }
            return Task.CompletedTask;
        });

        // Store in HttpContext.Items for access elsewhere
        context.Items[HeaderName] = correlationId;

        // Push correlation ID into structured logging scope
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await _next(context);
        }
    }

    private static string GetOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var correlationIdHeader) &&
            !string.IsNullOrWhiteSpace(correlationIdHeader.ToString()))
        {
            return correlationIdHeader.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
