using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
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

            // Resolve requestId from client (X-Correlation-Id / X-Request-Id / requestId) or fallback to traceId
            const string correlationHeader = "X-Correlation-Id";
            string requestId = traceId;
            if (context.Request.Headers.TryGetValue(correlationHeader, out var clientCorrId) &&
                !string.IsNullOrWhiteSpace(clientCorrId))
            {
                requestId = clientCorrId.ToString();
            }
            else if (context.Request.Headers.TryGetValue("X-Request-Id", out var clientReqId) &&
                     !string.IsNullOrWhiteSpace(clientReqId))
            {
                requestId = clientReqId.ToString();
            }
            else if (context.Request.Headers.TryGetValue("requestId", out var clientReqId2) &&
                     !string.IsNullOrWhiteSpace(clientReqId2))
            {
                requestId = clientReqId2.ToString();
            }

            // Propagate on request headers for downstream calls
            context.Request.Headers[correlationHeader] = requestId;
            context.Request.Headers["X-Request-Id"] = requestId;

            // Expose traceId and requestId in HTTP response headers
            context.Response.Headers["traceId"] = traceId;
            context.Response.Headers["requestId"] = requestId;
            context.Response.Headers["X-Trace-Id"] = traceId;
            context.Response.Headers["X-Request-Id"] = requestId;
            context.Response.Headers[correlationHeader] = requestId;

            // Buffer response to inject traceId & requestId into successful JSON object bodies
            var originalBodyStream = context.Response.Body;
            await using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            try
            {
                await next();

                memoryStream.Position = 0;
                var contentType = context.Response.ContentType;

                if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300 &&
                    contentType != null && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) &&
                    memoryStream.Length > 0)
                {
                    try
                    {
                        var jsonNode = System.Text.Json.Nodes.JsonNode.Parse(memoryStream);
                        if (jsonNode is System.Text.Json.Nodes.JsonObject jsonObject)
                        {
                            if (!jsonObject.ContainsKey("traceId"))
                            {
                                jsonObject["traceId"] = traceId;
                            }
                            if (!jsonObject.ContainsKey("requestId"))
                            {
                                jsonObject["requestId"] = requestId;
                            }

                            var newBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(jsonObject);
                            context.Response.ContentLength = newBytes.Length;
                            memoryStream.SetLength(0);
                            await memoryStream.WriteAsync(newBytes);
                            memoryStream.Position = 0;
                        }
                        else
                        {
                            memoryStream.Position = 0;
                        }
                    }
                    catch
                    {
                        memoryStream.Position = 0;
                    }
                }

                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(originalBodyStream);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        });
    }
}
