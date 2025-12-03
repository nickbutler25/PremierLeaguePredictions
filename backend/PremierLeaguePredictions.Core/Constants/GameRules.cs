namespace PremierLeaguePredictions.Core.Constants;

public static class GameRules
{
    // Points awarded for match results
    public const int PointsForWin = 3;
    public const int PointsForDraw = 1;
    public const int PointsForLoss = 0;

    // Season structure
    public const int FirstHalfStart = 1;
    public const int FirstHalfEnd = 19;
    public const int SecondHalfStart = 20;
    public const int SecondHalfEnd = 38;
    public const int TotalGameweeks = 38;

    // Half identifiers
    public const int FirstHalf = 1;
    public const int SecondHalf = 2;

    // Pick requirements
    public const int MaxPicksPerSeason = 38;
    public const int MinPicksForStandings = 1;

    // Helper method to determine which half a gameweek belongs to
    public static int GetHalfForGameweek(int gameweekNumber)
    {
        return gameweekNumber <= FirstHalfEnd ? FirstHalf : SecondHalf;
    }

    // Helper method to get the start of a half
    public static int GetHalfStart(int half)
    {
        return half == FirstHalf ? FirstHalfStart : SecondHalfStart;
    }

    // Helper method to get the end of a half
    public static int GetHalfEnd(int half)
    {
        return half == FirstHalf ? FirstHalfEnd : SecondHalfEnd;
    }
}
