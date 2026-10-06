using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PromptQuest.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBestPromptLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BestPromptLength",
                table: "LevelProgresses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_LevelProgresses_LevelId_Completed_BestPromptLength",
                table: "LevelProgresses",
                columns: new[] { "LevelId", "Completed", "BestPromptLength" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LevelProgresses_LevelId_Completed_BestPromptLength",
                table: "LevelProgresses");

            migrationBuilder.DropColumn(
                name: "BestPromptLength",
                table: "LevelProgresses");
        }
    }
}
