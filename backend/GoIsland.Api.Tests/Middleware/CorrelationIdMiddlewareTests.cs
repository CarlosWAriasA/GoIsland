using GoIsland.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoIsland.Api.Tests.Middleware;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task UsesSafeIncomingIdAndReturnsItInTheResponse()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "demo-request-123";
        var middleware = CreateMiddleware(next: _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.Equal("demo-request-123", context.TraceIdentifier);
        Assert.Equal(
            "demo-request-123",
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task ReplacesUnsafeIncomingId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "unsafe\r\nvalue";
        var middleware = CreateMiddleware(next: _ => Task.CompletedTask);

        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        Assert.NotEqual("unsafe\r\nvalue", context.TraceIdentifier);
        Assert.NotEmpty(context.TraceIdentifier);
        Assert.Equal(
            context.TraceIdentifier,
            context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    private static CorrelationIdMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<CorrelationIdMiddleware>.Instance);
}
