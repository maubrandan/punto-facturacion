using System.Linq;

namespace POS.API.Middleware;

/// <summary>
/// Propaga o genera <c>X-Request-Id</c> para correlación (consola plataforma / observabilidad).
/// </summary>
public sealed class RequestCorrelationMiddleware
{
    public const string HeaderName = "X-Request-Id";

    private readonly RequestDelegate _next;

    public RequestCorrelationMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = id;
        return _next(context);
    }
}
