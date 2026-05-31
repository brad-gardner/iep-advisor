using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class MeetingDateAndDropSuggestedQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuggestedQuestions",
                table: "IepAnalyses");

            migrationBuilder.DropColumn(
                name: "SuggestedQuestions",
                table: "EtrAnalyses");

            migrationBuilder.AddColumn<DateTime>(
                name: "MeetingDate",
                table: "MeetingPrepChecklists",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MeetingDate",
                table: "MeetingPrepChecklists");

            migrationBuilder.AddColumn<string>(
                name: "SuggestedQuestions",
                table: "IepAnalyses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuggestedQuestions",
                table: "EtrAnalyses",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
