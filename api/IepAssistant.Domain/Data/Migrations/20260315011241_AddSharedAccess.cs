using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChildAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChildProfileId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvitedByUserId = table.Column<int>(type: "int", nullable: true),
                    InviteEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    InviteToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InviteExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildAccesses_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChildAccesses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildAccesses_ChildProfileId",
                table: "ChildAccesses",
                column: "ChildProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildAccesses_ChildProfileId_UserId",
                table: "ChildAccesses",
                columns: new[] { "ChildProfileId", "UserId" },
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChildAccesses_InviteToken",
                table: "ChildAccesses",
                column: "InviteToken");

            migrationBuilder.CreateIndex(
                name: "IX_ChildAccesses_UserId",
                table: "ChildAccesses",
                column: "UserId");

            // Seed Owner access records for all existing child profiles
            migrationBuilder.Sql(@"
                INSERT INTO ChildAccesses (ChildProfileId, UserId, Role, AcceptedAt, IsActive, CreatedAt, UpdatedAt)
                SELECT Id, UserId, 'Owner', GETUTCDATE(), 1, GETUTCDATE(), GETUTCDATE()
                FROM ChildProfiles
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChildAccesses");
        }
    }
}
