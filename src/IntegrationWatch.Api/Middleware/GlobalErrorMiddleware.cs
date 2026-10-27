using System.Text.Json;
using IntegrationWatch.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IntegrationWatch.Api.Middleware;

public class GlobalErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalErrorMiddleware> _logger;

    public GlobalErrorMiddleware(RequestDelegate next, ILogger<GlobalErrorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        const string correlationHeader = "X-Correlation-ID";
        if (!context.Request.Headers.TryGetValue(correlationHeader, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }
        context.Response.Headers[correlationHeader] = correlationId;
        context.Items[correlationHeader] = correlationId.ToString();

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, correlationId.ToString());
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        int statusCode;
        string title;
        string detail;

        switch (exception)
        {
            case ResourceNotFoundException notFound:
                statusCode = StatusCodes.Status404NotFound;
                title = "Resource Not Found";
                detail = notFound.Message;
                _logger.LogWarning(exception, "[Correlation: {CorrelationId}] Resource not found: {Message}", correlationId, notFound.Message);
                break;

            case EndpointValidationException validation:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Endpoint Validation Error";
                detail = validation.Message;
                _logger.LogWarning(exception, "[Correlation: {CorrelationId}] Validation error: {Message}", correlationId, validation.Message);
                break;

            case IntegrationWatchException domainEx:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Application Domain Error";
                detail = domainEx.Message;
                _logger.LogWarning(exception, "[Correlation: {CorrelationId}] Domain error: {Message}", correlationId, domainEx.Message);
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "An unexpected server error occurred";
                // Prevent information disclosure of system internals (CWE-209)
                detail = "An internal error occurred. Please reference the correlation ID when reporting this issue.";
                _logger.LogError(exception, "[Correlation: {CorrelationId}] Unhandled exception: {Message}", correlationId, exception.Message);
                break;
        }

        var problem = new ProblemDetails
        {
            Type = "https://datatracker.ietf.org/doc/html/rfc9457#section-3",
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
