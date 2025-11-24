using System.Net;
using System.Text.Json;
using Npgsql;

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
                    message = exception.Message,
                    type = exception.GetType().Name
                });
                break;

            case UnauthorizedAccessException:
                code = HttpStatusCode.Unauthorized;
                result = JsonSerializer.Serialize(new
                {
                    error = "Unauthorized",
                    message = exception.Message ?? "You are not authorized to access this resource",
                    type = exception.GetType().Name
                });
                break;

            case KeyNotFoundException:
                code = HttpStatusCode.NotFound;
                result = JsonSerializer.Serialize(new
                {
                    error = "Not Found",
                    message = exception.Message,
                    type = exception.GetType().Name
                });
                break;

            case InvalidOperationException:
                code = HttpStatusCode.BadRequest;
                result = JsonSerializer.Serialize(new
                {
                    error = "Invalid Operation",
                    message = exception.Message,
                    type = exception.GetType().Name
                });
                break;

            case PostgresException postgresEx:
                // Check for common database errors
                var (statusCode, dbMessage) = postgresEx.SqlState switch
                {
                    "42P01" => (HttpStatusCode.InternalServerError, $"Database table does not exist: {postgresEx.TableName}. Please ensure database migrations are up to date."),
                    "23505" => (HttpStatusCode.Conflict, "This request has already been submitted."),
                    "23503" => (HttpStatusCode.BadRequest, "Cannot delete this record because it is referenced by other records."),
                    "23502" => (HttpStatusCode.BadRequest, "A required value is missing."),
                    _ => (HttpStatusCode.InternalServerError, "A database error occurred. Please contact support if the problem persists.")
                };

                code = statusCode;
                result = JsonSerializer.Serialize(new
                {
                    error = code == HttpStatusCode.Conflict ? "Conflict" : "Database Error",
                    message = dbMessage,
                    type = exception.GetType().Name,
                    sqlState = postgresEx.SqlState
                });
                break;

            default:
                result = JsonSerializer.Serialize(new
                {
                    error = "Internal Server Error",
                    message = "An error occurred while processing your request. Please try again or contact support if the problem persists.",
                    type = exception.GetType().Name
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
