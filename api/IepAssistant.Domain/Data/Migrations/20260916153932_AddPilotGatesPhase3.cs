using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPilotGatesPhase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDemo",
                table: "Districts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MagicLinkEnabled",
                table: "Districts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireMfaForMagicLink",
                table: "Districts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "MagicLinkTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(88)", maxLength: 88, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MagicLinkTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MagicLinkTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MagicLinkTokens_TokenHash",
                table: "MagicLinkTokens",
                column: "TokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_MagicLinkTokens_UserId_CreatedAt",
                table: "MagicLinkTokens",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MagicLinkTokens");

            migrationBuilder.DropColumn(
                name: "IsDemo",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "MagicLinkEnabled",
                table: "Districts");

            migrationBuilder.DropColumn(
                name: "RequireMfaForMagicLink",
                table: "Districts");
        }
    }
}
