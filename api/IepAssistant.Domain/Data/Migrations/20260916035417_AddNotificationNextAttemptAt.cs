using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationNextAttemptAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_EmailQueuedAt_EmailSentAt_EmailAttempts",
                table: "Notifications");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EmailQueuedAt_EmailSentAt_EmailAttempts_NextAttemptAt",
                table: "Notifications",
                columns: new[] { "EmailQueuedAt", "EmailSentAt", "EmailAttempts", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_EmailQueuedAt_EmailSentAt_EmailAttempts_NextAttemptAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EmailQueuedAt_EmailSentAt_EmailAttempts",
                table: "Notifications",
                columns: new[] { "EmailQueuedAt", "EmailSentAt", "EmailAttempts" });
        }
    }
}
