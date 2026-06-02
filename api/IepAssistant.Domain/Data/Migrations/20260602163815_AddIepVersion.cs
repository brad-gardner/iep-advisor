using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIepVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IepVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: false),
                    SourceDraftId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizedByUserId = table.Column<int>(type: "int", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepVersions_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IepVersionAccommodations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepVersionId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepVersionAccommodations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepVersionAccommodations_IepVersions_IepVersionId",
                        column: x => x.IepVersionId,
                        principalTable: "IepVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepVersionGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepVersionId = table.Column<int>(type: "int", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    GoalText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Baseline = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TargetCriteria = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MeasurementMethod = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Timeframe = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepVersionGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepVersionGoals_IepVersions_IepVersionId",
                        column: x => x.IepVersionId,
                        principalTable: "IepVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepVersionPdfs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepVersionId = table.Column<int>(type: "int", nullable: false),
                    BlobUri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Checksum = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RenderedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RenderStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepVersionPdfs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepVersionPdfs_IepVersions_IepVersionId",
                        column: x => x.IepVersionId,
                        principalTable: "IepVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepVersionSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepVersionId = table.Column<int>(type: "int", nullable: false),
                    SectionKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RichText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepVersionSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepVersionSections_IepVersions_IepVersionId",
                        column: x => x.IepVersionId,
                        principalTable: "IepVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepVersionServiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepVersionId = table.Column<int>(type: "int", nullable: false),
                    ServiceType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Frequency = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Duration = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderRole = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepVersionServiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepVersionServiceLines_IepVersions_IepVersionId",
                        column: x => x.IepVersionId,
                        principalTable: "IepVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IepVersionTransitionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IepVersionId = table.Column<int>(type: "int", nullable: false),
                    PostsecondaryGoalArea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ServicesText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IepVersionTransitionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IepVersionTransitionItems_IepVersions_IepVersionId",
                        column: x => x.IepVersionId,
                        principalTable: "IepVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionAccommodations_IepVersionId",
                table: "IepVersionAccommodations",
                column: "IepVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionAccommodations_LineageId",
                table: "IepVersionAccommodations",
                column: "LineageId");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionGoals_IepVersionId",
                table: "IepVersionGoals",
                column: "IepVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionGoals_LineageId",
                table: "IepVersionGoals",
                column: "LineageId");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionPdfs_IepVersionId",
                table: "IepVersionPdfs",
                column: "IepVersionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IepVersions_SchoolStudentId_VersionNumber",
                table: "IepVersions",
                columns: new[] { "SchoolStudentId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionSections_IepVersionId",
                table: "IepVersionSections",
                column: "IepVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionSections_LineageId",
                table: "IepVersionSections",
                column: "LineageId");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionServiceLines_IepVersionId",
                table: "IepVersionServiceLines",
                column: "IepVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionServiceLines_LineageId",
                table: "IepVersionServiceLines",
                column: "LineageId");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionTransitionItems_IepVersionId",
                table: "IepVersionTransitionItems",
                column: "IepVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IepVersionTransitionItems_LineageId",
                table: "IepVersionTransitionItems",
                column: "LineageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IepVersionAccommodations");

            migrationBuilder.DropTable(
                name: "IepVersionGoals");

            migrationBuilder.DropTable(
                name: "IepVersionPdfs");

            migrationBuilder.DropTable(
                name: "IepVersionSections");

            migrationBuilder.DropTable(
                name: "IepVersionServiceLines");

            migrationBuilder.DropTable(
                name: "IepVersionTransitionItems");

            migrationBuilder.DropTable(
                name: "IepVersions");
        }
    }
}
