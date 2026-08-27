using PremierLeaguePredictions.Application.DTOs;

namespace PremierLeaguePredictions.Application.Interfaces;

public interface IUserProfileService
{
    /// <summary>
    /// A player's season as another player is allowed to see it: their record, their revealed
    /// picks, what they have left to pick from, and how they compare with the viewer.
    /// </summary>
    /// <param name="userId">The player being looked at.</param>
    /// <param name="viewerId">Who is looking. Drives the head-to-head, omitted when they match.</param>
    /// <param name="seasonId">Defaults to the active season.</param>
    /// <returns>Null when the user does not exist.</returns>
    Task<UserProfileDto?> GetUserProfileAsync(
        Guid userId, Guid viewerId, string? seasonId = null, CancellationToken cancellationToken = default);
}
