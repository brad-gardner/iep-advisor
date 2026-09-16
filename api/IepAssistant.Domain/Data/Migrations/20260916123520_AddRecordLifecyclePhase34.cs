using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordLifecyclePhase34 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AmendmentReason",
                table: "DocumentInstances",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendsVersionId",
                table: "DocumentInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveDate",
                table: "DocumentInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AmendmentReason",
                table: "AuthoredDocumentVersions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AmendsVersionId",
                table: "AuthoredDocumentVersions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EffectiveDate",
                table: "AuthoredDocumentVersions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureStatus",
                table: "AuthoredDocumentVersions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unsigned");

            migrationBuilder.CreateTable(
                name: "ExportJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DistrictId = table.Column<int>(type: "int", nullable: false),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: true),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StudentCount = table.Column<int>(type: "int", nullable: false),
                    FileCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportJobs_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExportJobs_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FamilyContactAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyContactAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyContactAttempts_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingBriefs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeetingId = table.Column<int>(type: "int", nullable: false),
                    BriefJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceRevisionId = table.Column<int>(type: "int", nullable: true),
                    SourceVersionId = table.Column<int>(type: "int", nullable: true),
                    GeneratedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingBriefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingBriefs_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeetingId = table.Column<int>(type: "int", nullable: false),
                    TargetFieldKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetRowId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TargetLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingDecisions_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfflineFamilyInputs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: false),
                    DocumentInstanceId = table.Column<int>(type: "int", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineFamilyInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfflineFamilyInputs_DocumentInstances_DocumentInstanceId",
                        column: x => x.DocumentInstanceId,
                        principalTable: "DocumentInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflineFamilyInputs_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignatureEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthoredDocumentVersionId = table.Column<int>(type: "int", nullable: false),
                    SignerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SignerRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatureEvents_AuthoredDocumentVersions_AuthoredDocumentVersionId",
                        column: x => x.AuthoredDocumentVersionId,
                        principalTable: "AuthoredDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignedArtifacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthoredDocumentVersionId = table.Column<int>(type: "int", nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "int", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignerSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignedArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignedArtifacts_AuthoredDocumentVersions_AuthoredDocumentVersionId",
                        column: x => x.AuthoredDocumentVersionId,
                        principalTable: "AuthoredDocumentVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentInstances_AmendsVersionId",
                table: "DocumentInstances",
                column: "AmendsVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredDocumentVersions_AmendsVersionId",
                table: "AuthoredDocumentVersions",
                column: "AmendsVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthoredDocumentVersions_SchoolStudentId_SignatureStatus_FinalizedAt",
                table: "AuthoredDocumentVersions",
                columns: new[] { "SchoolStudentId", "SignatureStatus", "FinalizedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_DistrictId_Status",
                table: "ExportJobs",
                columns: new[] { "DistrictId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_SchoolStudentId",
                table: "ExportJobs",
                column: "SchoolStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyContactAttempts_SchoolStudentId_AttemptedAt",
                table: "FamilyContactAttempts",
                columns: new[] { "SchoolStudentId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingBriefs_MeetingId",
                table: "MeetingBriefs",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingDecisions_AppliedAt",
                table: "MeetingDecisions",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingDecisions_MeetingId",
                table: "MeetingDecisions",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineFamilyInputs_DocumentInstanceId",
                table: "OfflineFamilyInputs",
                column: "DocumentInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineFamilyInputs_SchoolStudentId_ReceivedAt",
                table: "OfflineFamilyInputs",
                columns: new[] { "SchoolStudentId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SignatureEvents_AuthoredDocumentVersionId",
                table: "SignatureEvents",
                column: "AuthoredDocumentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SignedArtifacts_AuthoredDocumentVersionId",
                table: "SignedArtifacts",
                column: "AuthoredDocumentVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuthoredDocumentVersions_AuthoredDocumentVersions_AmendsVersionId",
                table: "AuthoredDocumentVersions",
                column: "AmendsVersionId",
                principalTable: "AuthoredDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentInstances_AuthoredDocumentVersions_AmendsVersionId",
                table: "DocumentInstances",
                column: "AmendsVersionId",
                principalTable: "AuthoredDocumentVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuthoredDocumentVersions_AuthoredDocumentVersions_AmendsVersionId",
                table: "AuthoredDocumentVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentInstances_AuthoredDocumentVersions_AmendsVersionId",
                table: "DocumentInstances");

            migrationBuilder.DropTable(
                name: "ExportJobs");

            migrationBuilder.DropTable(
                name: "FamilyContactAttempts");

            migrationBuilder.DropTable(
                name: "MeetingBriefs");

            migrationBuilder.DropTable(
                name: "MeetingDecisions");

            migrationBuilder.DropTable(
                name: "OfflineFamilyInputs");

            migrationBuilder.DropTable(
                name: "SignatureEvents");

            migrationBuilder.DropTable(
                name: "SignedArtifacts");

            migrationBuilder.DropIndex(
                name: "IX_DocumentInstances_AmendsVersionId",
                table: "DocumentInstances");

            migrationBuilder.DropIndex(
                name: "IX_AuthoredDocumentVersions_AmendsVersionId",
                table: "AuthoredDocumentVersions");

            migrationBuilder.DropIndex(
                name: "IX_AuthoredDocumentVersions_SchoolStudentId_SignatureStatus_FinalizedAt",
                table: "AuthoredDocumentVersions");

            migrationBuilder.DropColumn(
                name: "AmendmentReason",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "AmendsVersionId",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "DocumentInstances");

            migrationBuilder.DropColumn(
                name: "AmendmentReason",
                table: "AuthoredDocumentVersions");

            migrationBuilder.DropColumn(
                name: "AmendsVersionId",
                table: "AuthoredDocumentVersions");

            migrationBuilder.DropColumn(
                name: "EffectiveDate",
                table: "AuthoredDocumentVersions");

            migrationBuilder.DropColumn(
                name: "SignatureStatus",
                table: "AuthoredDocumentVersions");
        }
    }
}
