using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisRunBackfillKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackfillSourceKey",
                table: "AnalysisRuns",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRuns_BackfillSourceKey",
                table: "AnalysisRuns",
                column: "BackfillSourceKey",
                unique: true,
                filter: "[BackfillSourceKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnalysisRuns_BackfillSourceKey",
                table: "AnalysisRuns");

            migrationBuilder.DropColumn(
                name: "BackfillSourceKey",
                table: "AnalysisRuns");
        }
    }
}
