using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEtrDocumentIdToMeetingPrepChecklist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EtrDocumentId",
                table: "MeetingPrepChecklists",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPrepChecklists_EtrDocumentId",
                table: "MeetingPrepChecklists",
                column: "EtrDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingPrepChecklists_EtrDocuments_EtrDocumentId",
                table: "MeetingPrepChecklists",
                column: "EtrDocumentId",
                principalTable: "EtrDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingPrepChecklists_EtrDocuments_EtrDocumentId",
                table: "MeetingPrepChecklists");

            migrationBuilder.DropIndex(
                name: "IX_MeetingPrepChecklists_EtrDocumentId",
                table: "MeetingPrepChecklists");

            migrationBuilder.DropColumn(
                name: "EtrDocumentId",
                table: "MeetingPrepChecklists");
        }
    }
}
