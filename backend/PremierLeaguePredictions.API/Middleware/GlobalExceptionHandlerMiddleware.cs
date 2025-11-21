using System.Net;
using System.Text.Json;

namespace PremierLeaguePredictions.API.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = HttpStatusCode.InternalServerError;
        var result = string.Empty;

        switch (exception)
        {
            case ArgumentNullException:
            case ArgumentException:
                code = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(new
                {
                    error = "Bad Request",
                    message = exception.Message
                });
                break;

            case UnauthorizedAccessException:
                code = HttpStatusCode.Unauthorized;
                result = JsonSerializer.Serialize(new
                {
                    error = "Unauthorized",
                    message = "You are not authorized to access this resource"
                });
                break;

            case KeyNotFoundException:
                code = HttpStatusCode.NotFound;
                result = JsonSerializer.Serialize(new
                {
                    error = "Not Found",
                    message = exception.Message
                });
                break;

            case InvalidOperationException:
                code = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(new
                {
                    error = "Invalid Operation",
                    message = exception.Message
                });
                break;

            default:
                result = JsonSerializer.Serialize(new
                {
                    error = "Internal Server Error",
                    message = "An error occurred while processing your request"
                });
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;
        return context.Response.WriteAsync(result);
    }
}

public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
