using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIepDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IepDrafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastEditedByUserId = table.Column<int>(type: "int", nullable: true),
                    LastEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepDrafts_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepDraftAccommodations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepDraftId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastEditedByUserId = table.Column<int>(type: "int", nullable: true),
                    LastEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepDraftAccommodations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepDraftAccommodations_IepDrafts_IepDraftId",
                        column: x => x.IepDraftId,
                        principalTable: "IepDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepDraftGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepDraftId = table.Column<int>(type: "int", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    GoalText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Baseline = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TargetCriteria = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MeasurementMethod = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Timeframe = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastEditedByUserId = table.Column<int>(type: "int", nullable: true),
                    LastEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepDraftGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepDraftGoals_IepDrafts_IepDraftId",
                        column: x => x.IepDraftId,
                        principalTable: "IepDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepDraftSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepDraftId = table.Column<int>(type: "int", nullable: false),
                    SectionKind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RichText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastEditedByUserId = table.Column<int>(type: "int", nullable: true),
                    LastEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepDraftSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepDraftSections_IepDrafts_IepDraftId",
                        column: x => x.IepDraftId,
                        principalTable: "IepDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepDraftServiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepDraftId = table.Column<int>(type: "int", nullable: false),
                    ServiceType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Duration = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderRole = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastEditedByUserId = table.Column<int>(type: "int", nullable: true),
                    LastEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepDraftServiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepDraftServiceLines_IepDrafts_IepDraftId",
                        column: x => x.IepDraftId,
                        principalTable: "IepDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepDraftTransitionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepDraftId = table.Column<int>(type: "int", nullable: false),
                    PostsecondaryGoalArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ServicesText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastEditedByUserId = table.Column<int>(type: "int", nullable: true),
                    LastEditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepDraftTransitionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepDraftTransitionItems_IepDrafts_IepDraftId",
                        column: x => x.IepDraftId,
                        principalTable: "IepDrafts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftAccommodations_IepDraftId",
                table: "IepDraftAccommodations",
                column: "IepDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftAccommodations_LineageId",
                table: "IepDraftAccommodations",
                column: "LineageId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftGoals_IepDraftId",
                table: "IepDraftGoals",
                column: "IepDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftGoals_LineageId",
                table: "IepDraftGoals",
                column: "LineageId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDrafts_SchoolStudentId",
                table: "IepDrafts",
                column: "SchoolStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftSections_IepDraftId",
                table: "IepDraftSections",
                column: "IepDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftSections_LineageId",
                table: "IepDraftSections",
                column: "LineageId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftServiceLines_IepDraftId",
                table: "IepDraftServiceLines",
                column: "IepDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftServiceLines_LineageId",
                table: "IepDraftServiceLines",
                column: "LineageId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftTransitionItems_IepDraftId",
                table: "IepDraftTransitionItems",
                column: "IepDraftId");

            migrationBuilder.CreateIndex(
                name: "IX_IepDraftTransitionItems_LineageId",
                table: "IepDraftTransitionItems",
                column: "LineageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IepDraftAccommodations");

            migrationBuilder.DropTable(
                name: "IepDraftGoals");

            migrationBuilder.DropTable(
                name: "IepDraftSections");

            migrationBuilder.DropTable(
                name: "IepDraftServiceLines");

            migrationBuilder.DropTable(
                name: "IepDraftTransitionItems");

            migrationBuilder.DropTable(
                name: "IepDrafts");
        }
    }
}
