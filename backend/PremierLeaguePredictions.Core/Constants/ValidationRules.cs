namespace PremierLeaguePredictions.Core.Constants;

public static class ValidationRules
{
    // Name length constraints
    public const int MaxNameLength = 100;
    public const int MaxFirstNameLength = 100;
    public const int MaxLastNameLength = 100;

    // Email constraints
    public const int MaxEmailLength = 255;

    // Season constraints
    public const int MaxSeasonIdLength = 50;
    public const int MaxSeasonNameLength = 50;

    // General constraints
    public const int MinPasswordLength = 8;
}
