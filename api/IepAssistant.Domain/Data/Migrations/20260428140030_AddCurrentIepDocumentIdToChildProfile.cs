using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentIepDocumentIdToChildProfile : Migration
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

            migrationBuilder.AddColumn<int>(
                name: "CurrentIepDocumentId",
                table: "ChildProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_CurrentIepDocumentId",
                table: "ChildProfiles",
                column: "CurrentIepDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChildProfiles_IepDocuments_CurrentIepDocumentId",
                table: "ChildProfiles",
                column: "CurrentIepDocumentId",
                principalTable: "IepDocuments",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChildProfiles_IepDocuments_CurrentIepDocumentId",
                table: "ChildProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ChildProfiles_CurrentIepDocumentId",
                table: "ChildProfiles");

            migrationBuilder.DropColumn(
                name: "AdvocacyGapAnalysis",
                table: "EtrAnalyses");

            migrationBuilder.DropColumn(
                name: "ParentGoalsSnapshot",
                table: "EtrAnalyses");

            migrationBuilder.DropColumn(
                name: "CurrentIepDocumentId",
                table: "ChildProfiles");
        }
    }
}
