using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisRunSourceLookupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRunSources_SourceType_SourceId",
                table: "AnalysisRunSources",
                columns: new[] { "SourceType", "SourceId" })
                .Annotation("SqlServer:Include", new[] { "AnalysisRunId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AnalysisRunSources_SourceType_SourceId",
                table: "AnalysisRunSources");
        }
    }
}
