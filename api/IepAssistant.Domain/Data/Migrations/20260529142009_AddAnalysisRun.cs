using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnalysisRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildProfileId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OverallSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    CrossDocSynthesis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallRedFlags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdvocacyGapAnalysis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentGoalsSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisRuns_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisRunSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalysisRunId = table.Column<int>(type: "int", nullable: false),
                    AnalysisRunSourceId = table.Column<int>(type: "int", nullable: true),
                    SectionKind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Analysis = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRunSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisRunSections_AnalysisRuns_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "AnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisRunSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AnalysisRunId = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourceContentSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRunSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisRunSources_AnalysisRuns_AnalysisRunId",
                        column: x => x.AnalysisRunId,
                        principalTable: "AnalysisRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRuns_ChildProfileId",
                table: "AnalysisRuns",
                column: "ChildProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRunSections_AnalysisRunId",
                table: "AnalysisRunSections",
                column: "AnalysisRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRunSections_AnalysisRunSourceId",
                table: "AnalysisRunSections",
                column: "AnalysisRunSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRunSources_AnalysisRunId",
                table: "AnalysisRunSources",
                column: "AnalysisRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisRunSections");

            migrationBuilder.DropTable(
                name: "AnalysisRunSources");

            migrationBuilder.DropTable(
                name: "AnalysisRuns");
        }
    }
}
