using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PremierLeaguePredictions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable uuid-ossp extension for UUID generation
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS \"uuid-ossp\";");

            migrationBuilder.CreateTable(
                name: "seasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_seasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    short_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    external_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_teams", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    photo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    google_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_paid = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gameweeks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    week_number = table.Column<int>(type: "integer", nullable: false),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_locked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gameweeks", x => x.id);
                    table.ForeignKey(
                        name: "FK_gameweeks_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "team_selections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    half = table.Column<int>(type: "integer", nullable: false),
                    gameweek_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_team_selections", x => x.id);
                    table.ForeignKey(
                        name: "FK_team_selections_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_team_selections_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_team_selections_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admin_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    admin_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_gameweek_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_admin_actions_gameweeks_target_gameweek_id",
                        column: x => x.target_gameweek_id,
                        principalTable: "gameweeks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_actions_users_admin_user_id",
                        column: x => x.admin_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_admin_actions_users_target_user_id",
                        column: x => x.target_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gameweek_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "SENT"),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_email_notifications_gameweeks_gameweek_id",
                        column: x => x.gameweek_id,
                        principalTable: "gameweeks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_email_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fixtures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    gameweek_id = table.Column<Guid>(type: "uuid", nullable: false),
                    home_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    away_team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kickoff_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    home_score = table.Column<int>(type: "integer", nullable: true),
                    away_score = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "SCHEDULED"),
                    external_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fixtures", x => x.id);
                    table.ForeignKey(
                        name: "FK_fixtures_gameweeks_gameweek_id",
                        column: x => x.gameweek_id,
                        principalTable: "gameweeks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fixtures_teams_away_team_id",
                        column: x => x.away_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_fixtures_teams_home_team_id",
                        column: x => x.home_team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "picks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gameweek_id = table.Column<Guid>(type: "uuid", nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    goals_for = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    goals_against = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_auto_assigned = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_picks", x => x.id);
                    table.ForeignKey(
                        name: "FK_picks_gameweeks_gameweek_id",
                        column: x => x.gameweek_id,
                        principalTable: "gameweeks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_picks_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_picks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_admin_user_id",
                table: "admin_actions",
                column: "admin_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_target_gameweek_id",
                table: "admin_actions",
                column: "target_gameweek_id");

            migrationBuilder.CreateIndex(
                name: "IX_admin_actions_target_user_id",
                table: "admin_actions",
                column: "target_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_notifications_gameweek_id",
                table: "email_notifications",
                column: "gameweek_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_notifications_user_id",
                table: "email_notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_away_team_id",
                table: "fixtures",
                column: "away_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_external_id",
                table: "fixtures",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_gameweek_id",
                table: "fixtures",
                column: "gameweek_id");

            migrationBuilder.CreateIndex(
                name: "IX_fixtures_home_team_id",
                table: "fixtures",
                column: "home_team_id");

            migrationBuilder.CreateIndex(
                name: "IX_gameweeks_season_id_week_number",
                table: "gameweeks",
                columns: new[] { "season_id", "week_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_picks_gameweek_id",
                table: "picks",
                column: "gameweek_id");

            migrationBuilder.CreateIndex(
                name: "IX_picks_team_id",
                table: "picks",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_picks_user_id_gameweek_id",
                table: "picks",
                columns: new[] { "user_id", "gameweek_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_team_selections_season_id",
                table: "team_selections",
                column: "season_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_selections_team_id",
                table: "team_selections",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "IX_team_selections_user_id_season_id_team_id_half",
                table: "team_selections",
                columns: new[] { "user_id", "season_id", "team_id", "half" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_teams_external_id",
                table: "teams",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_google_id",
                table: "users",
                column: "google_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_actions");

            migrationBuilder.DropTable(
                name: "email_notifications");

            migrationBuilder.DropTable(
                name: "fixtures");

            migrationBuilder.DropTable(
                name: "picks");

            migrationBuilder.DropTable(
                name: "team_selections");

            migrationBuilder.DropTable(
                name: "gameweeks");

            migrationBuilder.DropTable(
                name: "teams");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "seasons");
        }
    }
}
