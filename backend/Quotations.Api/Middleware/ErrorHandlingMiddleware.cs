using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace Quotations.Api.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IHostEnvironment env)
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
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        int statusCode;
        string message;

        switch (exception)
        {
            case InvalidOperationException:
                statusCode = StatusCodes.Status400BadRequest;
                message = exception.Message;
                break;
            case ArgumentNullException:
            case ArgumentException:
                statusCode = StatusCodes.Status400BadRequest;
                message = "Invalid request parameters.";
                break;
            case KeyNotFoundException:
                statusCode = StatusCodes.Status404NotFound;
                message = "The requested resource was not found.";
                break;
            case UnauthorizedAccessException:
                statusCode = StatusCodes.Status401Unauthorized;
                message = "Unauthorized.";
                break;
            default:
                statusCode = StatusCodes.Status500InternalServerError;
                message = _env.IsDevelopment() ? exception.Message : "An unexpected error occurred.";
                break;
        }

        var result = JsonSerializer.Serialize(
            new
            {
                success = false,
                errors = new Dictionary<string, string[]> { { "general", new[] { message } } }
            },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        return context.Response.WriteAsync(result);
    }
}
