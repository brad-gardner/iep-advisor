using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEtrAnalyses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EtrAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EtrDocumentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssessmentCompleteness = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EligibilityReview = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallRedFlags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedQuestions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EtrAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EtrAnalyses_EtrDocuments_EtrDocumentId",
                        column: x => x.EtrDocumentId,
                        principalTable: "EtrDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EtrAnalyses_EtrDocumentId",
                table: "EtrAnalyses",
                column: "EtrDocumentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EtrAnalyses");
        }
    }
}
