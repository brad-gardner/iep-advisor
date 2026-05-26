using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressReportAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProgressReportAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProgressReportId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    GoalProgressFindings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RedFlags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdvocacyGapAnalysis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentGoalsSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IepGoalsSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgressReportAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProgressReportAnalyses_ProgressReports_ProgressReportId",
                        column: x => x.ProgressReportId,
                        principalTable: "ProgressReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgressReportAnalyses_ProgressReportId",
                table: "ProgressReportAnalyses",
                column: "ProgressReportId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgressReportAnalyses");
        }
    }
}
