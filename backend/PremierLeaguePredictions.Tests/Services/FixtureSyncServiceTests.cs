using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PremierLeaguePredictions.Core.Entities;
using PremierLeaguePredictions.Infrastructure.Data;
using PremierLeaguePredictions.Infrastructure.Repositories;
using PremierLeaguePredictions.Infrastructure.Services;
using Xunit;

namespace PremierLeaguePredictions.Tests.Services;

public class FixtureSyncServiceTests
{
    private static ApplicationDbContext NewInMemoryContext() =>
        new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options
        );

    private static Team Team(string name, int externalId, bool isActive) =>
        new()
        {
            Name = name,
            ExternalId = externalId,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task SyncTeamsAsync_DeactivatesRelegatedTeams_AndReactivatesPromoted()
    {
        // Arrange: three existing teams — one stays up, one is relegated (drops out of the
        // current team list), one was previously inactive but has been promoted back.
        using var context = NewInMemoryContext();
        context.Teams.AddRange(
            Team("Arsenal", externalId: 1, isActive: true),
            Team("Relegated FC", externalId: 99, isActive: true),
            Team("Promoted FC", externalId: 50, isActive: false)
        );
        await context.SaveChangesAsync();

        // The current season's teams from the API: Arsenal + Promoted FC (no Relegated FC).
        var footballData = new Mock<IFootballDataService>();
        footballData
            .Setup(x => x.GetTeamsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new List<ExternalTeam>
                {
                    new()
                    {
                        Id = 1,
                        Name = "Arsenal",
                        ShortName = "Arsenal",
                        Tla = "ARS",
                    },
                    new()
                    {
                        Id = 50,
                        Name = "Promoted FC",
                        ShortName = "Promoted",
                        Tla = "PRO",
                    },
                }
            );

        var service = new FixtureSyncService(
            footballData.Object,
            new UnitOfWork(context),
            NullLogger<FixtureSyncService>.Instance,
            new MemoryCache(new MemoryCacheOptions())
        );

        // Act
        await service.SyncTeamsAsync();

        // Assert
        var isActiveByExternalId = await context.Teams.ToDictionaryAsync(
            t => t.ExternalId,
            t => t.IsActive
        );
        isActiveByExternalId[1].Should().BeTrue("Arsenal is still in the league");
        isActiveByExternalId[99].Should().BeFalse("Relegated FC is no longer in the current team list");
        isActiveByExternalId[50].Should().BeTrue("Promoted FC has returned and should be reactivated");
    }
}
