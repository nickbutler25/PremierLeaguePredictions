using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremierLeaguePredictions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnableRLSOnMigrationsHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable Row Level Security on __EFMigrationsHistory table
            // This resolves Supabase security warnings about RLS not being enabled
            migrationBuilder.Sql(@"
                ALTER TABLE ""__EFMigrationsHistory"" ENABLE ROW LEVEL SECURITY;
            ");

            // Create a policy that allows all operations
            // Since this is a system table used only by the backend application,
            // we allow all operations for PUBLIC (works with both standard PostgreSQL and Supabase)
            migrationBuilder.Sql(@"
                CREATE POLICY ""Allow all operations""
                ON ""__EFMigrationsHistory""
                FOR ALL
                TO PUBLIC
                USING (true)
                WITH CHECK (true);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the RLS policy
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS ""Allow all operations"" ON ""__EFMigrationsHistory"";
            ");

            // Disable Row Level Security
            migrationBuilder.Sql(@"
                ALTER TABLE ""__EFMigrationsHistory"" DISABLE ROW LEVEL SECURITY;
            ");
        }
    }
}
