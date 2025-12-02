using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremierLeaguePredictions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSeasonIdType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fix season_id columns that were incorrectly created as uuid instead of varchar
            // This migration converts them to the correct character varying(100) type

            migrationBuilder.Sql(@"
                -- Convert season_participations.season_id from uuid to varchar
                ALTER TABLE season_participations
                ALTER COLUMN season_id TYPE character varying(100) USING season_id::text;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: Convert back to uuid (only if values are valid UUIDs)
            migrationBuilder.Sql(@"
                ALTER TABLE season_participations
                ALTER COLUMN season_id TYPE uuid USING season_id::uuid;
            ");
        }
    }
}
