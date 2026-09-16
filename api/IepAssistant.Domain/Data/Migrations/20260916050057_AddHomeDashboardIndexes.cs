using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeDashboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudents_AnnualReviewDueDate",
                table: "SchoolStudents",
                column: "AnnualReviewDueDate");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudents_ReevaluationDueDate",
                table: "SchoolStudents",
                column: "ReevaluationDueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_StartsAtUtc",
                table: "Meetings",
                column: "StartsAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SchoolStudents_AnnualReviewDueDate",
                table: "SchoolStudents");

            migrationBuilder.DropIndex(
                name: "IX_SchoolStudents_ReevaluationDueDate",
                table: "SchoolStudents");

            migrationBuilder.DropIndex(
                name: "IX_Meetings_StartsAtUtc",
                table: "Meetings");
        }
    }
}
