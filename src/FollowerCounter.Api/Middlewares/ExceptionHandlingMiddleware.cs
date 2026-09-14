using System.Net;
using System.Text.Json;
using FollowerCounter.Application.Exceptions;
using FollowerCounter.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FollowerCounter.Api.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["X-Correlation-Id"]?.ToString() ?? context.TraceIdentifier;

        var statusCode = HttpStatusCode.InternalServerError;
        var problemDetails = new ProblemDetails
        {
            Instance = context.Request.Path,
            Extensions = { ["traceId"] = correlationId }
        };

        switch (exception)
        {
            case ValidationException valEx:
                statusCode = HttpStatusCode.BadRequest;
                problemDetails.Status = (int)HttpStatusCode.BadRequest;
                problemDetails.Title = "Validation failed";
                problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1";
                problemDetails.Detail = valEx.Message;
                problemDetails.Extensions["errors"] = valEx.Errors;
                _logger.LogInformation(exception, "[{CorrelationId}] Validation error: {Message}", correlationId, valEx.Message);
                break;

            case UnauthorizedException unauthEx:
                statusCode = HttpStatusCode.Unauthorized;
                problemDetails.Status = (int)HttpStatusCode.Unauthorized;
                problemDetails.Title = "Unauthorized";
                problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7235#section-3.1";
                problemDetails.Detail = unauthEx.Message;
                _logger.LogWarning("[{CorrelationId}] Unauthorized access attempt: {Message}", correlationId, unauthEx.Message);
                break;

            case NotFoundException notFoundEx:
                statusCode = HttpStatusCode.NotFound;
                problemDetails.Status = (int)HttpStatusCode.NotFound;
                problemDetails.Title = "Resource not found";
                problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.4";
                problemDetails.Detail = notFoundEx.Message;
                _logger.LogInformation("[{CorrelationId}] Resource not found: {Message}", correlationId, notFoundEx.Message);
                break;

            case ConflictException conflictEx:
                statusCode = HttpStatusCode.Conflict;
                problemDetails.Status = (int)HttpStatusCode.Conflict;
                problemDetails.Title = "Conflict";
                problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.8";
                problemDetails.Detail = conflictEx.Message;
                _logger.LogWarning("[{CorrelationId}] Conflict error: {Message}", correlationId, conflictEx.Message);
                break;

            case RateLimitException rateEx:
                statusCode = HttpStatusCode.TooManyRequests;
                problemDetails.Status = (int)HttpStatusCode.TooManyRequests;
                problemDetails.Title = "Rate limit exceeded";
                problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4";
                problemDetails.Detail = rateEx.Message;
                context.Response.Headers.RetryAfter = Math.Ceiling(rateEx.RetryAfter.TotalSeconds).ToString();
                _logger.LogWarning("[{CorrelationId}] Rate limit exceeded: {Message}", correlationId, rateEx.Message);
                break;

            case DomainException domainEx:
                statusCode = HttpStatusCode.BadRequest;
                problemDetails.Status = (int)HttpStatusCode.BadRequest;
                problemDetails.Title = "Business rule violation";
                problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1";
                problemDetails.Detail = domainEx.Message;
                _logger.LogWarning("[{CorrelationId}] Domain rule violation: {Message}", correlationId, domainEx.Message);
                break;

            default:
                statusCode = HttpStatusCode.InternalServerError;
                problemDetails.Status = (int)HttpStatusCode.InternalServerError;
                problemDetails.Title = "An unexpected error occurred.";
                problemDetails.Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1";
                problemDetails.Detail = _env.IsDevelopment()
                    ? exception.Message
                    : "An internal error occurred while processing your request. Please reference the traceId when contacting support.";
                _logger.LogError(exception, "[{CorrelationId}] Unhandled exception processing {Path}", correlationId, context.Request.Path);
                break;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }
}
