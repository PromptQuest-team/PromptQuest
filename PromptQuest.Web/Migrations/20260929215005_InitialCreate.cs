using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptQuest.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Attempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: true),
                    ElapsedMs = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attempts_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LevelProgresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LevelId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    BestAttempts = table.Column<int>(type: "integer", nullable: false),
                    BestTimeMs = table.Column<int>(type: "integer", nullable: false),
                    BestScore = table.Column<int>(type: "integer", nullable: false),
                    TotalAttempts = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevelProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LevelProgresses_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Attempts_PlayerId_LevelId",
                table: "Attempts",
                columns: new[] { "PlayerId", "LevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_LevelProgresses_LevelId_Completed_BestScore",
                table: "LevelProgresses",
                columns: new[] { "LevelId", "Completed", "BestScore" });

            migrationBuilder.CreateIndex(
                name: "IX_LevelProgresses_PlayerId_LevelId",
                table: "LevelProgresses",
                columns: new[] { "PlayerId", "LevelId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attempts");

            migrationBuilder.DropTable(
                name: "LevelProgresses");

            migrationBuilder.DropTable(
                name: "Players");
        }
    }
}
