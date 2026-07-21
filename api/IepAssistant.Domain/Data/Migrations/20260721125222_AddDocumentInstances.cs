using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: false),
                    DocumentTypeId = table.Column<int>(type: "int", nullable: false),
                    DocumentTemplateVersionId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    LastEditedByUserId = table.Column<int>(type: "int", nullable: true),
                    LastEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentInstances_DocumentTemplateVersions_DocumentTemplateVersionId",
                        column: x => x.DocumentTemplateVersionId,
                        principalTable: "DocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentInstances_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentInstances_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInstances_DocumentTemplateVersionId",
                table: "DocumentInstances",
                column: "DocumentTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInstances_DocumentTypeId",
                table: "DocumentInstances",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInstances_SchoolStudentId",
                table: "DocumentInstances",
                column: "SchoolStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInstances_SchoolStudentId_DocumentTypeId",
                table: "DocumentInstances",
                columns: new[] { "SchoolStudentId", "DocumentTypeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentInstances");
        }
    }
}
