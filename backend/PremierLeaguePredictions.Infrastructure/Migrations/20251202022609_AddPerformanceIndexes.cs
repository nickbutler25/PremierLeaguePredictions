using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremierLeaguePredictions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Composite index for fixture queries by status and kickoff time
            // Optimizes queries like: WHERE status = 'FINISHED' ORDER BY kickoff_time
            migrationBuilder.Sql(
                "CREATE INDEX idx_fixtures_status_kickoff ON fixtures(status, kickoff_time);");

            // 2. Composite index for pick lookups
            // Optimizes queries like: WHERE user_id = X AND season_id = Y AND gameweek_number = Z
            migrationBuilder.Sql(
                "CREATE INDEX idx_picks_user_season_gameweek ON picks(user_id, season_id, gameweek_number);");

            // 3. Partial index for admin users (only indexes admin users)
            // Optimizes queries like: WHERE is_admin = true AND is_active = true
            migrationBuilder.Sql(
                "CREATE INDEX idx_users_admin_active ON users(is_admin, is_active) WHERE is_admin = true;");

            // 4. Partial index for auto-assigned picks (only indexes auto-picks)
            // Optimizes queries like: WHERE is_auto_assigned = true
            migrationBuilder.Sql(
                "CREATE INDEX idx_picks_auto_assigned ON picks(is_auto_assigned) WHERE is_auto_assigned = true;");

            // 5. Composite index for elimination queries
            // Optimizes queries like: WHERE season_id = X AND gameweek_number = Y
            migrationBuilder.Sql(
                "CREATE INDEX idx_user_eliminations_season_gameweek ON user_eliminations(season_id, gameweek_number);");

            // 6. Index for season participation lookups
            // Optimizes queries like: WHERE season_id = X AND is_approved = true
            migrationBuilder.Sql(
                "CREATE INDEX idx_season_participations_season_approved ON season_participations(season_id, is_approved);");

            // 7. Index for picks by season (for standings calculations)
            // Optimizes queries like: WHERE season_id = X
            migrationBuilder.Sql(
                "CREATE INDEX idx_picks_season ON picks(season_id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_fixtures_status_kickoff;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_picks_user_season_gameweek;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_users_admin_active;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_picks_auto_assigned;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_user_eliminations_season_gameweek;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_season_participations_season_approved;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_picks_season;");
        }
    }
}
