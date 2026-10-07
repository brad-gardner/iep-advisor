using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAiArtifactLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "SharedDraftExplanations",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "ProgressReportAnalyses",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "ParentDraftNotes",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "MeetingSummaries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "MeetingPrepChecklists",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "AnalysisRuns",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "AdvocateMessages",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "SharedDraftExplanations");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "ProgressReportAnalyses");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "ParentDraftNotes");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "MeetingSummaries");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "MeetingPrepChecklists");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "AnalysisRuns");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "AdvocateMessages");
        }
    }
}
