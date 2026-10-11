using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Shared.Exceptions.Handler;

// global exception handler buat bungkus error jadi format problem details (rfc 7807)
public class CustomExceptionHandler(
    ILogger<CustomExceptionHandler> logger,
    IHostEnvironment? env = null)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        const string correlationHeader = "X-Correlation-Id";
        var correlationId = ResolveCorrelationId(httpContext.Request.Headers, traceId);

        httpContext.Response.Headers["X-Trace-Id"] = traceId;
        httpContext.Response.Headers[correlationHeader] = correlationId;

        logger.LogError(
            exception,
            "Error Message: {exceptionMessage}, TraceId: {traceId}, CorrelationId: {correlationId}, Time: {time}",
            exception.Message, traceId, correlationId, DateTime.UtcNow);

        var details = MapExceptionDetails(exception);

        httpContext.Response.StatusCode = details.StatusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Type = details.Type,
            Title = details.Title,
            Detail = details.Detail,
            Status = details.StatusCode,
            Instance = httpContext.Request.Path
        };

        EnrichProblemDetails(problemDetails, exception, traceId, correlationId);

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
        return true;
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

    private (string Detail, string Title, int StatusCode, string Type) MapExceptionDetails(Exception exception)
    {
        return exception switch
        {
            ValidationException =>
            (
                exception.Message,
                "Validation Error",
                StatusCodes.Status400BadRequest,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            ),
            BadRequestException =>
            (
                exception.Message,
                "Bad Request",
                StatusCodes.Status400BadRequest,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            ),
            NotFoundException =>
            (
                exception.Message,
                "Resource Not Found",
                StatusCodes.Status404NotFound,
                "https://tools.ietf.org/html/rfc7231#section-6.5.4"
            ),
            ConflictException =>
            (
                exception.Message,
                "Conflict",
                StatusCodes.Status409Conflict,
                "https://tools.ietf.org/html/rfc7231#section-6.5.8"
            ),
            DbUpdateException dbUpdateEx when IsDuplicateKeyException(dbUpdateEx) =>
            (
                "Data yang dikirim bentrok dengan data yang sudah ada (duplikat unique key).",
                "Conflict",
                StatusCodes.Status409Conflict,
                "https://tools.ietf.org/html/rfc7231#section-6.5.8"
            ),
            InternalServerException =>
            (
                exception.Message,
                "Internal Server Error",
                StatusCodes.Status500InternalServerError,
                "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            ),
            _ =>
            (
                env != null && env.IsDevelopment()
                    ? exception.Message
                    : "Terjadi kesalahan internal pada server. Silakan hubungi admin dengan menyertakan TraceId.",
                "Internal Server Error",
                StatusCodes.Status500InternalServerError,
                "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            )
        };
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message;
        return msg != null && (msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                               msg.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase));
    }

    private static void EnrichProblemDetails(ProblemDetails problemDetails, Exception exception, string traceId, string correlationId)
    {
        problemDetails.Extensions.Add("traceId", traceId);
        problemDetails.Extensions.Add("correlationId", correlationId);

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions.Add("ValidationErrors", validationException.Errors);
        }

        var customDetails = exception switch
        {
            BadRequestException badRequestEx => badRequestEx.Details,
            ConflictException conflictEx => conflictEx.Details,
            InternalServerException internalEx => internalEx.Details,
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(customDetails))
        {
            problemDetails.Extensions.Add("details", customDetails);
        }
    }
}
