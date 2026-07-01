using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogActorCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AccessAuditLogs_ActorUserId_CreatedAt",
                table: "AccessAuditLogs",
                columns: new[] { "ActorUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessAuditLogs_ActorUserId_CreatedAt",
                table: "AccessAuditLogs");
        }
    }
}
