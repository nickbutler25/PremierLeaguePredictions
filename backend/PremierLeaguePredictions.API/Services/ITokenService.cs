using PremierLeaguePredictions.API.Models;

namespace PremierLeaguePredictions.API.Services;

public interface ITokenService
{
    string GenerateToken(User user);
    Guid? ValidateToken(string token);
}
