using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ElixomClaim.Web.Configuration;

public static class RateLimitingConfiguration
{
    public const string OAuthPolicy = "oauth";
    public const string McpPolicy = "mcp";
    public const string MvcPolicy = "mvc";

    public static IServiceCollection AddApplicationRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "too_many_requests",
                    error_description = "Rate limit exceeded. Please try again later."
                }, cancellationToken);
            };

            // OAuth policy: 20 requests per minute per client key
            options.AddPolicy(OAuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIdentityKey(httpContext, "oauth"),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // MCP policy: 60 requests per minute per client key
            options.AddPolicy(McpPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIdentityKey(httpContext, "mcp"),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // MVC policy: 100 requests per minute per client key
            options.AddPolicy(MvcPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: GetClientIdentityKey(httpContext, "mvc"),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    public static string GetClientIdentityKey(HttpContext httpContext, string prefix)
    {
        var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            return $"{prefix}:user:{userId}";
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(ip))
        {
            return $"{prefix}:ip:{ip}";
        }

        return $"{prefix}:anonymous";
    }
}
