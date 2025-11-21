using PremierLeaguePredictions.Core.Entities;

namespace PremierLeaguePredictions.Infrastructure.Services;

public interface ITokenService
{
    string GenerateToken(User user);
    Guid? ValidateToken(string token);
}
