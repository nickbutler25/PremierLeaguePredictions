using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremierLeaguePredictions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "teams",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "teams");
        }
    }
}
