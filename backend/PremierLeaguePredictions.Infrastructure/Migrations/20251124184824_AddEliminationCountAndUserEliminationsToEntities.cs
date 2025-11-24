using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremierLeaguePredictions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEliminationCountAndUserEliminationsToEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "elimination_count",
                table: "gameweeks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "user_eliminations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gameweek_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    total_points = table.Column<int>(type: "integer", nullable: false),
                    eliminated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    eliminated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_eliminations", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_eliminations_gameweeks_gameweek_id",
                        column: x => x.gameweek_id,
                        principalTable: "gameweeks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_eliminations_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_eliminations_users_eliminated_by",
                        column: x => x.eliminated_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_eliminations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_eliminations_eliminated_by",
                table: "user_eliminations",
                column: "eliminated_by");

            migrationBuilder.CreateIndex(
                name: "IX_user_eliminations_gameweek_id",
                table: "user_eliminations",
                column: "gameweek_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_eliminations_season_id",
                table: "user_eliminations",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_eliminations_user_id_season_id",
                table: "user_eliminations",
                columns: new[] { "user_id", "season_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_eliminations");

            migrationBuilder.DropColumn(
                name: "elimination_count",
                table: "gameweeks");
        }
    }
}
