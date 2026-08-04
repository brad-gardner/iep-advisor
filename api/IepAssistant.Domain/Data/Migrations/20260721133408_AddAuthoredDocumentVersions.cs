using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthoredDocumentVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthoredDocumentVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    DocumentTemplateVersionId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    ValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FinalizedByUserId = table.Column<int>(type: "int", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthoredDocumentVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthoredDocumentVersions_DocumentTemplateVersions_DocumentTemplateVersionId",
                        column: x => x.DocumentTemplateVersionId,
                        principalTable: "DocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthoredDocumentVersions_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuthoredDocumentVersions_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuthoredDocumentPdfs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthoredDocumentVersionId = table.Column<int>(type: "int", nullable: false),
                    RenderStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RenderedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthoredDocumentPdfs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthoredDocumentPdfs_AuthoredDocumentVersions_AuthoredDocumentVersionId",
                        column: x => x.AuthoredDocumentVersionId,
                        principalTable: "AuthoredDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredDocumentPdfs_AuthoredDocumentVersionId",
                table: "AuthoredDocumentPdfs",
                column: "AuthoredDocumentVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredDocumentVersions_DocumentTemplateVersionId",
                table: "AuthoredDocumentVersions",
                column: "DocumentTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredDocumentVersions_DocumentTypeId",
                table: "AuthoredDocumentVersions",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredDocumentVersions_SchoolStudentId_DocumentTypeId_VersionNumber",
                table: "AuthoredDocumentVersions",
                columns: new[] { "SchoolStudentId", "DocumentTypeId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthoredDocumentPdfs");

            migrationBuilder.DropTable(
                name: "AuthoredDocumentVersions");
        }
    }
}
