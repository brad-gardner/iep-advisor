using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingPrepChecklists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingPrepChecklists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildProfileId = table.Column<int>(type: "int", nullable: false),
                    IepDocumentId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QuestionsToAsk = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentsToBring = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RedFlagsToRaise = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RightsToReference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GoalGaps = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneralTips = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingPrepChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingPrepChecklists_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingPrepChecklists_IepDocuments_IepDocumentId",
                        column: x => x.IepDocumentId,
                        principalTable: "IepDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPrepChecklists_ChildProfileId",
                table: "MeetingPrepChecklists",
                column: "ChildProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingPrepChecklists_IepDocumentId",
                table: "MeetingPrepChecklists",
                column: "IepDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingPrepChecklists");
        }
    }
}
