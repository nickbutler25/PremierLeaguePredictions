namespace PremierLeaguePredictions.Application.DTOs;

/// <summary>
/// Standard API response wrapper for consistent response format across all endpoints
/// </summary>
public class ApiResponse<T>
{
    /// <summary>
    /// Indicates if the request was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Response data (null if request failed)
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Success or error message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// List of validation or error messages (null if successful)
    /// </summary>
    public List<string>? Errors { get; set; }

    /// <summary>
    /// Timestamp of the response
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful response with data
    /// </summary>
    public static ApiResponse<T> SuccessResult(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message ?? "Request successful"
        };
    }

    /// <summary>
    /// Creates a successful response without data
    /// </summary>
    public static ApiResponse<T> SuccessResult(string message)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = default,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failure response with error message
    /// </summary>
    public static ApiResponse<T> FailureResult(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = message,
            Errors = errors
        };
    }

    /// <summary>
    /// Creates a validation failure response
    /// </summary>
    public static ApiResponse<T> ValidationFailure(List<string> errors)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = "Validation failed",
            Errors = errors
        };
    }
}

/// <summary>
/// Non-generic API response for endpoints that don't return data
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    /// <summary>
    /// Creates a successful response without data
    /// </summary>
    public new static ApiResponse SuccessResult(string message)
    {
        return new ApiResponse
        {
            Success = true,
            Data = null,
            Message = message
        };
    }

    /// <summary>
    /// Creates a failure response
    /// </summary>
    public new static ApiResponse FailureResult(string message, List<string>? errors = null)
    {
        return new ApiResponse
        {
            Success = false,
            Data = null,
            Message = message,
            Errors = errors
        };
    }
}
