using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisRunSourceStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "AnalysisRunSources",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "AnalysisRunSources",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            // Data step: existing AnalysisRunSource rows predate per-source status tracking, so there
            // is no real per-source history to restore. Backfill each from its OWN run's status — a
            // Completed run's sources are treated as Completed (the run could not have completed
            // without them), everything else (Error/Pending/Running, i.e. every run that never
            // reached a successful terminal state) as Error. ErrorMessage is deliberately left NULL:
            // the original per-source failure reason, if any, was never captured.
            migrationBuilder.Sql(@"
                UPDATE ars
                SET ars.Status = CASE WHEN ar.Status = 'Completed' THEN 'Completed' ELSE 'Error' END
                FROM AnalysisRunSources ars
                INNER JOIN AnalysisRuns ar ON ar.Id = ars.AnalysisRunId;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "AnalysisRunSources");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AnalysisRunSources");
        }
    }
}
