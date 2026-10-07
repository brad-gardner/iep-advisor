using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IepVersionPdfs_IepVersionId",
                table: "IepVersionPdfs");

            migrationBuilder.DropIndex(
                name: "IX_AuthoredDocumentPdfs_AuthoredDocumentVersionId",
                table: "AuthoredDocumentPdfs");

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "IepVersionPdfs",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "AuthoredDocumentPdfs",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionPdfs_IepVersionId_Language",
                table: "IepVersionPdfs",
                columns: new[] { "IepVersionId", "Language" },
                unique: true,
                filter: "[Language] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredDocumentPdfs_AuthoredDocumentVersionId_Language",
                table: "AuthoredDocumentPdfs",
                columns: new[] { "AuthoredDocumentVersionId", "Language" },
                unique: true,
                filter: "[Language] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IepVersionPdfs_IepVersionId_Language",
                table: "IepVersionPdfs");

            migrationBuilder.DropIndex(
                name: "IX_AuthoredDocumentPdfs_AuthoredDocumentVersionId_Language",
                table: "AuthoredDocumentPdfs");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "IepVersionPdfs");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "AuthoredDocumentPdfs");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionPdfs_IepVersionId",
                table: "IepVersionPdfs",
                column: "IepVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredDocumentPdfs_AuthoredDocumentVersionId",
                table: "AuthoredDocumentPdfs",
                column: "AuthoredDocumentVersionId",
                unique: true);
        }
    }
}
