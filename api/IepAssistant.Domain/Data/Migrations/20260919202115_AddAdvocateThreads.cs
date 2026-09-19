using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvocateThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdvocateThreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildProfileId = table.Column<int>(type: "int", nullable: false),
                    ParentUserId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessageAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvocateThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvocateThreads_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdvocateThreads_Users_ParentUserId",
                        column: x => x.ParentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AdvocateMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdvocateThreadId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContentMarkdown = table.Column<string>(type: "nvarchar(max)", maxLength: 32000, nullable: false),
                    CitationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SuggestionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ToolTraceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    Truncated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvocateMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvocateMessages_AdvocateThreads_AdvocateThreadId",
                        column: x => x.AdvocateThreadId,
                        principalTable: "AdvocateThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvocateMessages_AdvocateThreadId_CreatedAt",
                table: "AdvocateMessages",
                columns: new[] { "AdvocateThreadId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdvocateThreads_ChildProfileId",
                table: "AdvocateThreads",
                column: "ChildProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvocateThreads_ParentUserId_ChildProfileId_LastMessageAt",
                table: "AdvocateThreads",
                columns: new[] { "ParentUserId", "ChildProfileId", "LastMessageAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvocateMessages");

            migrationBuilder.DropTable(
                name: "AdvocateThreads");
        }
    }
}
