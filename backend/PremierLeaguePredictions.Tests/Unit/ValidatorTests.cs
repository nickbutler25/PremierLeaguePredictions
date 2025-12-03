using FluentAssertions;
using FluentValidation.TestHelper;
using PremierLeaguePredictions.Application.DTOs;
using PremierLeaguePredictions.Application.Validators;
using PremierLeaguePredictions.Core.Constants;
using Xunit;

namespace PremierLeaguePredictions.Tests.Unit;

public class ValidatorTests
{
    public class RegisterRequestValidatorTests
    {
        private readonly RegisterRequestValidator _validator = new();

        [Fact]
        public void Email_WithValidLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = new string('a', ValidationRules.MaxEmailLength - 10) + "@test.com",
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Email);
        }

        [Fact]
        public void Email_ExceedingMaxLength_ShouldHaveValidationError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = new string('a', ValidationRules.MaxEmailLength + 1) + "@test.com",
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Email)
                .WithErrorMessage($"Email must not exceed {ValidationRules.MaxEmailLength} characters");
        }

        [Fact]
        public void FirstName_WithValidLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                FirstName = new string('a', ValidationRules.MaxFirstNameLength),
                LastName = "Doe"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.FirstName);
        }

        [Fact]
        public void FirstName_ExceedingMaxLength_ShouldHaveValidationError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                FirstName = new string('a', ValidationRules.MaxFirstNameLength + 1),
                LastName = "Doe"
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.FirstName)
                .WithErrorMessage($"First name must not exceed {ValidationRules.MaxFirstNameLength} characters");
        }

        [Fact]
        public void LastName_WithValidLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                FirstName = "John",
                LastName = new string('a', ValidationRules.MaxLastNameLength)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.LastName);
        }

        [Fact]
        public void LastName_ExceedingMaxLength_ShouldHaveValidationError()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                FirstName = "John",
                LastName = new string('a', ValidationRules.MaxLastNameLength + 1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.LastName)
                .WithErrorMessage($"Last name must not exceed {ValidationRules.MaxLastNameLength} characters");
        }
    }

    public class BackfillPickRequestValidatorTests
    {
        private readonly BackfillPickRequestValidator _validator = new();

        [Theory]
        [InlineData(1)]
        [InlineData(19)]
        [InlineData(20)]
        [InlineData(38)]
        public void GameweekNumber_WithinValidRange_ShouldNotHaveValidationError(int gameweekNumber)
        {
            // Arrange
            var request = new BackfillPickRequest
            {
                GameweekNumber = gameweekNumber,
                TeamId = 1
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.GameweekNumber);
        }

        [Fact]
        public void GameweekNumber_ExceedingMaxGameweeks_ShouldHaveValidationError()
        {
            // Arrange
            var request = new BackfillPickRequest
            {
                GameweekNumber = GameRules.TotalGameweeks + 1,
                TeamId = 1
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.GameweekNumber)
                .WithErrorMessage($"Gameweek number cannot exceed {GameRules.TotalGameweeks}");
        }

        [Fact]
        public void GameweekNumber_Zero_ShouldHaveValidationError()
        {
            // Arrange
            var request = new BackfillPickRequest
            {
                GameweekNumber = 0,
                TeamId = 1
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.GameweekNumber)
                .WithErrorMessage("Gameweek number must be greater than 0");
        }
    }

    public class CreateSeasonRequestValidatorTests
    {
        private readonly CreateSeasonRequestValidator _validator = new();

        [Fact]
        public void SeasonName_WithValidLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new CreateSeasonRequest
            {
                Name = new string('a', ValidationRules.MaxSeasonNameLength),
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(9),
                ExternalSeasonYear = 2024
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void SeasonName_ExceedingMaxLength_ShouldHaveValidationError()
        {
            // Arrange
            var request = new CreateSeasonRequest
            {
                Name = new string('a', ValidationRules.MaxSeasonNameLength + 1),
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(9),
                ExternalSeasonYear = 2024
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.Name)
                .WithErrorMessage($"Season name must not exceed {ValidationRules.MaxSeasonNameLength} characters");
        }
    }

    public class CreateSeasonParticipationRequestValidatorTests
    {
        private readonly CreateSeasonParticipationRequestValidator _validator = new();

        [Fact]
        public void SeasonId_WithValidLength_ShouldNotHaveValidationError()
        {
            // Arrange
            var request = new CreateSeasonParticipationRequest
            {
                SeasonId = new string('a', ValidationRules.MaxSeasonIdLength)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldNotHaveValidationErrorFor(x => x.SeasonId);
        }

        [Fact]
        public void SeasonId_ExceedingMaxLength_ShouldHaveValidationError()
        {
            // Arrange
            var request = new CreateSeasonParticipationRequest
            {
                SeasonId = new string('a', ValidationRules.MaxSeasonIdLength + 1)
            };

            // Act
            var result = _validator.TestValidate(request);

            // Assert
            result.ShouldHaveValidationErrorFor(x => x.SeasonId)
                .WithErrorMessage($"Season ID must not exceed {ValidationRules.MaxSeasonIdLength} characters");
        }
    }
}
