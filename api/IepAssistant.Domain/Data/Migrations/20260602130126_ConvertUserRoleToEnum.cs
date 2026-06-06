using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConvertUserRoleToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Map legacy role values onto the new UserRole enum names BEFORE the column is
            //    narrowed/read through the enum converter (avoids parse failures during rollout).
            migrationBuilder.Sql("UPDATE Users SET Role = 'Parent' WHERE Role = 'User'");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            // 2. Co-parent / multi-owner safety net: ensure every ChildProfile has an accepted
            //    Owner ChildAccess for its denormalized primary owner (legacy/seed rows may lack one).
            //    Idempotent — only inserts where no accepted Owner row already exists. No Down needed:
            //    these are legitimate authorization rows going forward.
            migrationBuilder.Sql(@"
                INSERT INTO ChildAccesses (ChildProfileId, UserId, Role, AcceptedAt, IsActive, CreatedAt, UpdatedAt)
                SELECT cp.Id, cp.UserId, 'Owner', GETUTCDATE(), 1, GETUTCDATE(), GETUTCDATE()
                FROM ChildProfiles cp
                WHERE NOT EXISTS (
                    SELECT 1 FROM ChildAccesses ca
                    WHERE ca.ChildProfileId = cp.Id
                      AND ca.UserId = cp.UserId
                      AND ca.Role = 'Owner'
                      AND ca.AcceptedAt IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "Users",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            // Restore the legacy "User" value for rows that were mapped to Parent.
            migrationBuilder.Sql("UPDATE Users SET Role = 'User' WHERE Role = 'Parent'");
        }
    }
}
