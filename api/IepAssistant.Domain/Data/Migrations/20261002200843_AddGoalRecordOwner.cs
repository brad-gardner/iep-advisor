using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalRecordOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OwnerUserId",
                table: "GoalRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoalRecords_OwnerUserId",
                table: "GoalRecords",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoalRecords_Users_OwnerUserId",
                table: "GoalRecords",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoalRecords_Users_OwnerUserId",
                table: "GoalRecords");

            migrationBuilder.DropIndex(
                name: "IX_GoalRecords_OwnerUserId",
                table: "GoalRecords");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "GoalRecords");
        }
    }
}
