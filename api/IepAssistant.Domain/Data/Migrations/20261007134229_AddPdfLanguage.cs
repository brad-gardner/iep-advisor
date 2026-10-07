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
            // Dropping back to the single-column unique index below only works if at most one row per
            // version remains — delete the non-English renders first (a downgrade is a deliberate,
            // destructive rollback of the multilingual-PDF feature; its Spanish/other-language rows have
            // no home in the pre-this-migration schema).
            migrationBuilder.Sql("DELETE FROM IepVersionPdfs WHERE Language IS NOT NULL AND Language <> 'en'");
            migrationBuilder.Sql("DELETE FROM AuthoredDocumentPdfs WHERE Language IS NOT NULL AND Language <> 'en'");

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
