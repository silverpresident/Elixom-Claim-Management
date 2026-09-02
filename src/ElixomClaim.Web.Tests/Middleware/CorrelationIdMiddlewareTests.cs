using ElixomClaim.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ElixomClaim.Web.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenHeaderIsMissing()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.True(context.Items.ContainsKey(CorrelationIdMiddleware.HeaderName));
        var correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
    }

    [Fact]
    public async Task InvokeAsync_PreservesIncomingCorrelationId_WhenHeaderIsPresent()
    {
        var customId = "custom-correlation-id-12345";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = customId;

        var middleware = new CorrelationIdMiddleware((ctx) => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(customId, context.Items[CorrelationIdMiddleware.HeaderName]);
    }
}
