using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIepAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IepAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepDocumentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SectionAnalyses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoalAnalyses = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OverallSummary = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: true),
                    OverallRedFlags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestedQuestions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepAnalyses_IepDocuments_IepDocumentId",
                        column: x => x.IepDocumentId,
                        principalTable: "IepDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IepAnalyses_IepDocumentId",
                table: "IepAnalyses",
                column: "IepDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IepAnalyses");
        }
    }
}
