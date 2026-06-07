using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class PendingStaffInviteUniqueEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffInvites_Email",
                table: "StaffInvites");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvites_Email",
                table: "StaffInvites",
                column: "Email",
                unique: true,
                filter: "IsActive = 1 AND AcceptedAt IS NULL AND InviteToken IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffInvites_Email",
                table: "StaffInvites");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvites_Email",
                table: "StaffInvites",
                column: "Email");
        }
    }
}
