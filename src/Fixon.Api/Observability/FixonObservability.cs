using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Fixon.Api.Observability;

public static class FixonObservability
{
    public const string ServiceName = "Fixon.Api";

    public static IServiceCollection AddFixonObservability(this IServiceCollection services, IConfiguration configuration)
    {
        // Structured JSON logs (no PII). Includes scopes so tenant/correlation show up.
        services.AddLogging(logging =>
        {
            logging.AddJsonConsole(o =>
            {
                o.IncludeScopes = true;
                o.TimestampFormat = "O";
            });
        });

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(opt =>
                    {
                        opt.EnrichWithHttpRequest = (activity, request) =>
                        {
                            // Safe tags
                            activity.SetTag("http.request_content_length", request.ContentLength);
                        };
                        opt.EnrichWithHttpResponse = (activity, response) =>
                        {
                            activity.SetTag("http.response_content_length", response.ContentLength);
                        };
                    })
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(); // OTEL_EXPORTER_OTLP_ENDPOINT drives destination
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter();
            });

        return services;
    }
}

