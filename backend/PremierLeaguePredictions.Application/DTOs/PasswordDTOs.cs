namespace PremierLeaguePredictions.Application.DTOs;

/// <summary>Start a reset: "I've forgotten my password, my email is this".</summary>
public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

/// <summary>Finish a reset, using the token from the emailed link.</summary>
public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Change a password from inside a signed-in session.</summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>
/// Why a reset attempt failed, for the caller to turn into a response.
/// </summary>
/// <remarks>
/// Expired and NotFound are deliberately separate even though the endpoint tells the user the
/// same thing either way. A link that has expired is the overwhelmingly common case and worth
/// seeing in the logs distinctly from one that never existed.
/// </remarks>
public enum PasswordResetOutcome
{
    Success,
    TokenNotFound,
    TokenExpired,
    TokenAlreadyUsed
}
