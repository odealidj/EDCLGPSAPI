using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Shared.Infrastructure.Telemetry;

public static class OpenTelemetryExtensions
{
    public const string DefaultActivitySourceName = "EDCL.GPS.Telematics";
    public static readonly ActivitySource ActivitySource = new(DefaultActivitySourceName);

    public static IServiceCollection AddEdclOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string serviceVersion = "1.0.0")
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"]
                           ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
                           ?? "http://localhost:4317";

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddTelemetrySdk()
            .AddEnvironmentVariableDetector();

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(DefaultActivitySourceName)
                    .AddSource(serviceName)
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.Filter = httpContext =>
                        {
                            // Filter out noisy health check probes
                            var path = httpContext.Request.Path.Value;
                            return path == null || !path.StartsWith("/hc") && !path.StartsWith("/health");
                        };
                    });

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(serviceName)
                    .AddMeter("edcl.gps.collector")
                    .AddPrometheusExporter();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(otlpEndpoint);
                    });
                }
            });

        return services;
    }

    public static IEndpointRouteBuilder MapEdclPrometheus(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPrometheusScrapingEndpoint();
        return endpoints;
    }

    public static IApplicationBuilder UseEdclTraceContext(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var activity = Activity.Current;
            var traceId = activity?.TraceId.ToString() ?? context.TraceIdentifier;
            const string correlationHeader = "X-Correlation-Id";
            var correlationId = ResolveCorrelationId(context.Request.Headers, traceId);

            context.Request.Headers[correlationHeader] = correlationId;
            context.Response.Headers["X-Trace-Id"] = traceId;
            context.Response.Headers[correlationHeader] = correlationId;

            var originalBodyStream = context.Response.Body;
            await using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            try
            {
                await next();

                TryInjectTraceInfo(memoryStream, context.Response, traceId, correlationId);

                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream, context.RequestAborted);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        });
    }

    private static string ResolveCorrelationId(IHeaderDictionary headers, string fallbackTraceId)
    {
        string[] candidateHeaders = ["X-Correlation-Id", "correlationId", "X-Request-Id", "requestId"];
        foreach (var header in candidateHeaders)
        {
            if (headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val.ToString();
            }
        }
        return fallbackTraceId;
    }

    private static void TryInjectTraceInfo(MemoryStream memoryStream, HttpResponse response, string traceId, string correlationId)
    {
        memoryStream.Position = 0;
        var contentType = response.ContentType;
        var isSuccessJson = response.StatusCode >= 200 && response.StatusCode < 300 &&
                            contentType != null && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) &&
                            memoryStream.Length > 0;

        if (!isSuccessJson) return;

        try
        {
            var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(memoryStream);
            if (jsonNode is System.Text.Json.Nodes.JsonObject jsonObject)
            {
                if (!jsonObject.ContainsKey("traceId"))
                {
                    jsonObject["traceId"] = traceId;
                }
                if (!jsonObject.ContainsKey("correlationId"))
                {
                    jsonObject["correlationId"] = correlationId;
                }

                memoryStream.SetLength(0);
                using (var writer = new System.Text.Json.Utf8JsonWriter(memoryStream))
                {
                    jsonObject.WriteTo(writer);
                }
                response.ContentLength = memoryStream.Length;
            }
        }
        catch
        {
            // Ignore parse errors and retain original stream
        }
        finally
        {
            memoryStream.Position = 0;
        }
    }
}
