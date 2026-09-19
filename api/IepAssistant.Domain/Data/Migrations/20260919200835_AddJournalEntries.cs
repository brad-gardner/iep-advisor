using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JournalEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildProfileId = table.Column<int>(type: "int", nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContentMarkdown = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    LinkedIepDocumentId = table.Column<int>(type: "int", nullable: true),
                    LinkedEtrDocumentId = table.Column<int>(type: "int", nullable: true),
                    LinkedMeetingId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntries_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalEntries_EtrDocuments_LinkedEtrDocumentId",
                        column: x => x.LinkedEtrDocumentId,
                        principalTable: "EtrDocuments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalEntries_IepDocuments_LinkedIepDocumentId",
                        column: x => x.LinkedIepDocumentId,
                        principalTable: "IepDocuments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_JournalEntries_Meetings_LinkedMeetingId",
                        column: x => x.LinkedMeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_ChildProfileId_OccurredOn",
                table: "JournalEntries",
                columns: new[] { "ChildProfileId", "OccurredOn" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_LinkedEtrDocumentId",
                table: "JournalEntries",
                column: "LinkedEtrDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_LinkedIepDocumentId",
                table: "JournalEntries",
                column: "LinkedIepDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_LinkedMeetingId",
                table: "JournalEntries",
                column: "LinkedMeetingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntries");
        }
    }
}
