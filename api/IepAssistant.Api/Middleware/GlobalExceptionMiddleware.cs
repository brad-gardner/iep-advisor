using System.Net;
using System.Text.Json;
using IepAssistant.Api.DTOs.Common;
using IepAssistant.Domain.Exceptions;

namespace IepAssistant.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
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

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            EntityNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            DomainException => (HttpStatusCode.BadRequest, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized"),
            // In dev, flatten the whole exception chain — the actionable cause (e.g. a SqlException
            // duplicate-key) lives in the InnerException, not the generic top-level message.
            _ => (HttpStatusCode.InternalServerError,
                _env.IsDevelopment() ? Flatten(exception) : "An internal error occurred")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse<object>.Error(message);

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private static string Flatten(Exception exception)
    {
        var messages = new List<string>();
        for (var ex = exception; ex != null; ex = ex.InnerException)
            messages.Add($"{ex.GetType().Name}: {ex.Message}");
        return string.Join(" -> ", messages);
    }
}
