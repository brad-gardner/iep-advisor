using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateSectionsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentTemplateVersions",
                type: "varbinary(max)",
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "rowversion",
                oldRowVersion: true,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "TemplateSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentTemplateVersionId = table.Column<int>(type: "int", nullable: false),
                    SectionKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateSections_DocumentTemplateVersions_DocumentTemplateVersionId",
                        column: x => x.DocumentTemplateVersionId,
                        principalTable: "DocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TemplateFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TemplateSectionId = table.Column<int>(type: "int", nullable: false),
                    DocumentTemplateVersionId = table.Column<int>(type: "int", nullable: false),
                    FieldKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplateFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemplateFields_DocumentTemplateVersions_DocumentTemplateVersionId",
                        column: x => x.DocumentTemplateVersionId,
                        principalTable: "DocumentTemplateVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TemplateFields_TemplateSections_TemplateSectionId",
                        column: x => x.TemplateSectionId,
                        principalTable: "TemplateSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplateVersions_DocumentTemplateId_VersionNumber",
                table: "DocumentTemplateVersions",
                columns: new[] { "DocumentTemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFields_DocumentTemplateVersionId",
                table: "TemplateFields",
                column: "DocumentTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFields_DocumentTemplateVersionId_FieldKey",
                table: "TemplateFields",
                columns: new[] { "DocumentTemplateVersionId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemplateFields_TemplateSectionId",
                table: "TemplateFields",
                column: "TemplateSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSections_DocumentTemplateVersionId",
                table: "TemplateSections",
                column: "DocumentTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TemplateSections_DocumentTemplateVersionId_SectionKey",
                table: "TemplateSections",
                columns: new[] { "DocumentTemplateVersionId", "SectionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TemplateFields");

            migrationBuilder.DropTable(
                name: "TemplateSections");

            migrationBuilder.DropIndex(
                name: "IX_DocumentTemplateVersions_DocumentTemplateId_VersionNumber",
                table: "DocumentTemplateVersions");

            migrationBuilder.AlterColumn<byte[]>(
                name: "RowVersion",
                table: "DocumentTemplateVersions",
                type: "rowversion",
                rowVersion: true,
                nullable: true,
                oldClrType: typeof(byte[]),
                oldType: "varbinary(max)",
                oldNullable: true);
        }
    }
}
