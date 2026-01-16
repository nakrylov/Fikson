using System.Diagnostics;

namespace Fixon.Api.Observability;

/// <summary>
/// Correlation-aware middleware:
/// - принимает входящий X-Correlation-Id или генерирует новый
/// - возвращает X-Correlation-Id в ответе
/// - не влияет на бизнес-логику (только делает поведение видимым)
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Activity tags are safe (no PII)
        Activity.Current?.SetTag("fixon.correlation_id", correlationId);

        await _next(context);
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.ToString();
        }

        // fallback: use trace id if present, else generate guid
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId;
    }
}

