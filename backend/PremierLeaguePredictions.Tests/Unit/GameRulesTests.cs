using FluentAssertions;
using PremierLeaguePredictions.Core.Constants;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

public class GameRulesTests
{
    [Fact]
    public void PointsForWin_ShouldBeThree()
    {
        // Assert
        GameRules.PointsForWin.Should().Be(3);
    }

    [Fact]
    public void PointsForDraw_ShouldBeOne()
    {
        // Assert
        GameRules.PointsForDraw.Should().Be(1);
    }

    [Fact]
    public void PointsForLoss_ShouldBeZero()
    {
        // Assert
        GameRules.PointsForLoss.Should().Be(0);
    }

    [Fact]
    public void FirstHalfStart_ShouldBeOne()
    {
        // Assert
        GameRules.FirstHalfStart.Should().Be(1);
    }

    [Fact]
    public void FirstHalfEnd_ShouldBeNineteen()
    {
        // Assert
        GameRules.FirstHalfEnd.Should().Be(19);
    }

    [Fact]
    public void SecondHalfStart_ShouldBeTwenty()
    {
        // Assert
        GameRules.SecondHalfStart.Should().Be(20);
    }

    [Fact]
    public void SecondHalfEnd_ShouldBeThirtyEight()
    {
        // Assert
        GameRules.SecondHalfEnd.Should().Be(38);
    }

    [Fact]
    public void TotalGameweeks_ShouldBeThirtyEight()
    {
        // Assert
        GameRules.TotalGameweeks.Should().Be(38);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(19, 1)]
    [InlineData(20, 2)]
    [InlineData(25, 2)]
    [InlineData(38, 2)]
    public void GetHalfForGameweek_ShouldReturnCorrectHalf(int gameweekNumber, int expectedHalf)
    {
        // Act
        var result = GameRules.GetHalfForGameweek(gameweekNumber);

        // Assert
        result.Should().Be(expectedHalf);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 20)]
    public void GetHalfStart_ShouldReturnCorrectStartWeek(int half, int expectedStart)
    {
        // Act
        var result = GameRules.GetHalfStart(half);

        // Assert
        result.Should().Be(expectedStart);
    }

    [Theory]
    [InlineData(1, 19)]
    [InlineData(2, 38)]
    public void GetHalfEnd_ShouldReturnCorrectEndWeek(int half, int expectedEnd)
    {
        // Act
        var result = GameRules.GetHalfEnd(half);

        // Assert
        result.Should().Be(expectedEnd);
    }

    [Fact]
    public void FirstHalf_ConstantShouldBeOne()
    {
        // Assert
        GameRules.FirstHalf.Should().Be(1);
    }

    [Fact]
    public void SecondHalf_ConstantShouldBeTwo()
    {
        // Assert
        GameRules.SecondHalf.Should().Be(2);
    }

    [Fact]
    public void MaxPicksPerSeason_ShouldEqualTotalGameweeks()
    {
        // Assert
        GameRules.MaxPicksPerSeason.Should().Be(GameRules.TotalGameweeks);
    }

    [Fact]
    public void MinPicksForStandings_ShouldBeOne()
    {
        // Assert
        GameRules.MinPicksForStandings.Should().Be(1);
    }

    [Fact]
    public void FirstHalfRange_ShouldSpanNineteenWeeks()
    {
        // Arrange
        var expectedRange = 19;

        // Act
        var actualRange = GameRules.FirstHalfEnd - GameRules.FirstHalfStart + 1;

        // Assert
        actualRange.Should().Be(expectedRange);
    }

    [Fact]
    public void SecondHalfRange_ShouldSpanNineteenWeeks()
    {
        // Arrange
        var expectedRange = 19;

        // Act
        var actualRange = GameRules.SecondHalfEnd - GameRules.SecondHalfStart + 1;

        // Assert
        actualRange.Should().Be(expectedRange);
    }

    [Fact]
    public void BothHalves_ShouldCoverAllGameweeks()
    {
        // Arrange
        var firstHalfWeeks = GameRules.FirstHalfEnd - GameRules.FirstHalfStart + 1;
        var secondHalfWeeks = GameRules.SecondHalfEnd - GameRules.SecondHalfStart + 1;

        // Act
        var totalWeeks = firstHalfWeeks + secondHalfWeeks;

        // Assert
        totalWeeks.Should().Be(GameRules.TotalGameweeks);
    }
}
