using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.API.Services;

public interface ITokenService
{
    string GenerateToken(User user);
    Guid? ValidateToken(string token);
}
