using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvocacyGoalSnapshotsToEtrAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdvocacyGapAnalysis",
                table: "EtrAnalyses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentGoalsSnapshot",
                table: "EtrAnalyses",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdvocacyGapAnalysis",
                table: "EtrAnalyses");

            migrationBuilder.DropColumn(
                name: "ParentGoalsSnapshot",
                table: "EtrAnalyses");
        }
    }
}
