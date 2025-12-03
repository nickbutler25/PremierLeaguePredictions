using FluentAssertions;
using PremierLeaguePredictions.Core.Constants;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

public class ValidationRulesTests
{
    [Fact]
    public void MaxNameLength_ShouldBeOneHundred()
    {
        // Assert
        ValidationRules.MaxNameLength.Should().Be(100);
    }

    [Fact]
    public void MaxFirstNameLength_ShouldBeOneHundred()
    {
        // Assert
        ValidationRules.MaxFirstNameLength.Should().Be(100);
    }

    [Fact]
    public void MaxLastNameLength_ShouldBeOneHundred()
    {
        // Assert
        ValidationRules.MaxLastNameLength.Should().Be(100);
    }

    [Fact]
    public void MaxEmailLength_ShouldBeTwoFiftyFive()
    {
        // Assert
        ValidationRules.MaxEmailLength.Should().Be(255);
    }

    [Fact]
    public void MaxSeasonIdLength_ShouldBeFifty()
    {
        // Assert
        ValidationRules.MaxSeasonIdLength.Should().Be(50);
    }

    [Fact]
    public void MaxSeasonNameLength_ShouldBeFifty()
    {
        // Assert
        ValidationRules.MaxSeasonNameLength.Should().Be(50);
    }

    [Fact]
    public void MinPasswordLength_ShouldBeEight()
    {
        // Assert
        ValidationRules.MinPasswordLength.Should().Be(8);
    }

    [Fact]
    public void AllNameLengths_ShouldBeEqual()
    {
        // Assert
        ValidationRules.MaxFirstNameLength.Should().Be(ValidationRules.MaxLastNameLength);
        ValidationRules.MaxFirstNameLength.Should().Be(ValidationRules.MaxNameLength);
    }

    [Fact]
    public void EmailLength_ShouldBeGreaterThanNameLength()
    {
        // Assert
        ValidationRules.MaxEmailLength.Should().BeGreaterThan(ValidationRules.MaxNameLength);
    }

    [Fact]
    public void SeasonIdAndNameLengths_ShouldBeEqual()
    {
        // Assert
        ValidationRules.MaxSeasonIdLength.Should().Be(ValidationRules.MaxSeasonNameLength);
    }
}
