using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordLifecyclePhase12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvaluationCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReferralDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferralSource = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConsentRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsentReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsentBlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConsentFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    DeterminationDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDateOverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EligibilityOutcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DeterminationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeterminationRationale = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EtrDocumentInstanceId = table.Column<int>(type: "int", nullable: true),
                    EtrAuthoredVersionId = table.Column<int>(type: "int", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluationCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluationCases_AuthoredDocumentVersions_EtrAuthoredVersionId",
                        column: x => x.EtrAuthoredVersionId,
                        principalTable: "AuthoredDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationCases_DocumentInstances_EtrDocumentInstanceId",
                        column: x => x.EtrDocumentInstanceId,
                        principalTable: "DocumentInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaluationCases_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthoredDocumentVersionId = table.Column<int>(type: "int", nullable: false),
                    DocumentInstanceId = table.Column<int>(type: "int", nullable: false),
                    FieldKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GoalText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Baseline = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TargetCriteria = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    MeasurementMethod = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Timeframe = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalRecords_AuthoredDocumentVersions_AuthoredDocumentVersionId",
                        column: x => x.AuthoredDocumentVersionId,
                        principalTable: "AuthoredDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalRecords_DocumentInstances_DocumentInstanceId",
                        column: x => x.DocumentInstanceId,
                        principalTable: "DocumentInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalRecords_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GoalRetirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentInstanceId = table.Column<int>(type: "int", nullable: false),
                    LineageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RetiredByUserId = table.Column<int>(type: "int", nullable: false),
                    RetiredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalRetirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalRetirements_DocumentInstances_DocumentInstanceId",
                        column: x => x.DocumentInstanceId,
                        principalTable: "DocumentInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EvaluatorAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EvaluationCaseId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaluatorAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaluatorAssignments_EvaluationCases_EvaluationCaseId",
                        column: x => x.EvaluationCaseId,
                        principalTable: "EvaluationCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EvaluatorAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GoalObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GoalRecordId = table.Column<int>(type: "int", nullable: false),
                    ObservedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GoalObservations_GoalRecords_GoalRecordId",
                        column: x => x.GoalRecordId,
                        principalTable: "GoalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCases_EtrAuthoredVersionId",
                table: "EvaluationCases",
                column: "EtrAuthoredVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCases_EtrDocumentInstanceId",
                table: "EvaluationCases",
                column: "EtrDocumentInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCases_OneOpenPerStudent",
                table: "EvaluationCases",
                column: "SchoolStudentId",
                unique: true,
                filter: "[Status] <> 'Closed'");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluationCases_SchoolStudentId_Status",
                table: "EvaluationCases",
                columns: new[] { "SchoolStudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaluatorAssignments_EvaluationCaseId",
                table: "EvaluatorAssignments",
                column: "EvaluationCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaluatorAssignments_UserId_SubmittedAt_DueDate",
                table: "EvaluatorAssignments",
                columns: new[] { "UserId", "SubmittedAt", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalObservations_GoalRecordId_ObservedAt",
                table: "GoalObservations",
                columns: new[] { "GoalRecordId", "ObservedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalRecords_AuthoredDocumentVersionId_LineageId",
                table: "GoalRecords",
                columns: new[] { "AuthoredDocumentVersionId", "LineageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoalRecords_DocumentInstanceId",
                table: "GoalRecords",
                column: "DocumentInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalRecords_SchoolStudentId_LineageId",
                table: "GoalRecords",
                columns: new[] { "SchoolStudentId", "LineageId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalRecords_SchoolStudentId_Status",
                table: "GoalRecords",
                columns: new[] { "SchoolStudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalRetirements_DocumentInstanceId_LineageId",
                table: "GoalRetirements",
                columns: new[] { "DocumentInstanceId", "LineageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaluatorAssignments");

            migrationBuilder.DropTable(
                name: "GoalObservations");

            migrationBuilder.DropTable(
                name: "GoalRetirements");

            migrationBuilder.DropTable(
                name: "EvaluationCases");

            migrationBuilder.DropTable(
                name: "GoalRecords");
        }
    }
}
