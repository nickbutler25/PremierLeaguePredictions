namespace PremierLeaguePredictions.API.Authorization;

/// <summary>
/// Defines authorization policy names for admin operations.
/// </summary>
public static class AdminPolicies
{
    /// <summary>
    /// Basic admin access - read-only operations and viewing data.
    /// Required for all admin endpoints.
    /// </summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Data modification - creating, updating, or deleting records.
    /// Used for operations like team management, gameweek recalculation.
    /// </summary>
    public const string DataModification = "DataModification";

    /// <summary>
    /// Critical operations - overriding picks, eliminations, backfilling data.
    /// Highest level of admin access for operations that directly affect user standings.
    /// </summary>
    public const string CriticalOperations = "CriticalOperations";

    /// <summary>
    /// External sync - synchronizing data with external APIs.
    /// Used for fixture sync, results sync operations.
    /// </summary>
    public const string ExternalSync = "ExternalSync";
}
