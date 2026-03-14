using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Users",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "State",
                table: "Users");

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "CreatedById", "Email", "FirstName", "IsActive", "LastName", "PasswordHash", "Role", "UpdatedAt", "UpdatedById" },
                values: new object[] { 1, new DateTime(2026, 1, 24, 20, 18, 38, 783, DateTimeKind.Utc), null, "admin@sht.dev", "Admin", true, "User", "$2a$11$Ll5ehmg7wnx/QNLdUwIwiObQr3axFZoQ6pHScnwYIClCFt4Ran57G", "Admin", new DateTime(2026, 1, 24, 20, 18, 38, 783, DateTimeKind.Utc), null });
        }
    }
}
