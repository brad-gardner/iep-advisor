using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyDraftSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ChildProfileId",
                table: "UsageRecords",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "UsageRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FamilyDraftSharingEnabled",
                table: "Districts",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "MeetingSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MeetingId = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentByUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingSummaries_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SharedDraftRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentInstanceId = table.Column<int>(type: "int", nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    ValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentTemplateVersionId = table.Column<int>(type: "int", nullable: false),
                    SharedByUserId = table.Column<int>(type: "int", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WithdrawnAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WithdrawnByUserId = table.Column<int>(type: "int", nullable: true),
                    ChangeSummaryJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedDraftRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedDraftRevisions_DocumentInstances_DocumentInstanceId",
                        column: x => x.DocumentInstanceId,
                        principalTable: "DocumentInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SharedDraftRevisions_DocumentTemplateVersions_DocumentTemplateVersionId",
                        column: x => x.DocumentTemplateVersionId,
                        principalTable: "DocumentTemplateVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SharedDraftRevisions_Users_SharedByUserId",
                        column: x => x.SharedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DraftAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SharedDraftRevisionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DraftAcknowledgements_SharedDraftRevisions_SharedDraftRevisionId",
                        column: x => x.SharedDraftRevisionId,
                        principalTable: "SharedDraftRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftAcknowledgements_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DraftResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SharedDraftRevisionId = table.Column<int>(type: "int", nullable: false),
                    ParentUserId = table.Column<int>(type: "int", nullable: false),
                    TargetFieldKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetRowId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResolvedByUserId = table.Column<int>(type: "int", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StaffReply = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedInDraft = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DraftResponses_SharedDraftRevisions_SharedDraftRevisionId",
                        column: x => x.SharedDraftRevisionId,
                        principalTable: "SharedDraftRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DraftResponses_Users_ParentUserId",
                        column: x => x.ParentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentDraftNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SharedDraftRevisionId = table.Column<int>(type: "int", nullable: false),
                    ParentUserId = table.Column<int>(type: "int", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetFieldKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetRowId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentDraftNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentDraftNotes_SharedDraftRevisions_SharedDraftRevisionId",
                        column: x => x.SharedDraftRevisionId,
                        principalTable: "SharedDraftRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentDraftNotes_Users_ParentUserId",
                        column: x => x.ParentUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SharedDraftExplanations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SharedDraftRevisionId = table.Column<int>(type: "int", nullable: false),
                    ExplanationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedDraftExplanations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SharedDraftExplanations_SharedDraftRevisions_SharedDraftRevisionId",
                        column: x => x.SharedDraftRevisionId,
                        principalTable: "SharedDraftRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsageRecords_DistrictId",
                table: "UsageRecords",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftAcknowledgements_SharedDraftRevisionId_UserId",
                table: "DraftAcknowledgements",
                columns: new[] { "SharedDraftRevisionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DraftAcknowledgements_UserId",
                table: "DraftAcknowledgements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftResponses_ParentUserId",
                table: "DraftResponses",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DraftResponses_SharedDraftRevisionId_ParentUserId",
                table: "DraftResponses",
                columns: new[] { "SharedDraftRevisionId", "ParentUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DraftResponses_SharedDraftRevisionId_Status",
                table: "DraftResponses",
                columns: new[] { "SharedDraftRevisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingSummaries_MeetingId",
                table: "MeetingSummaries",
                column: "MeetingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentDraftNotes_ParentUserId",
                table: "ParentDraftNotes",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentDraftNotes_SharedDraftRevisionId_ParentUserId",
                table: "ParentDraftNotes",
                columns: new[] { "SharedDraftRevisionId", "ParentUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedDraftExplanations_SharedDraftRevisionId",
                table: "SharedDraftExplanations",
                column: "SharedDraftRevisionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedDraftRevisions_DocumentInstanceId_RevisionNumber",
                table: "SharedDraftRevisions",
                columns: new[] { "DocumentInstanceId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SharedDraftRevisions_DocumentInstanceId_Status",
                table: "SharedDraftRevisions",
                columns: new[] { "DocumentInstanceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SharedDraftRevisions_DocumentTemplateVersionId",
                table: "SharedDraftRevisions",
                column: "DocumentTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedDraftRevisions_SharedByUserId",
                table: "SharedDraftRevisions",
                column: "SharedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UsageRecords_Districts_DistrictId",
                table: "UsageRecords",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UsageRecords_Districts_DistrictId",
                table: "UsageRecords");

            migrationBuilder.DropTable(
                name: "DraftAcknowledgements");

            migrationBuilder.DropTable(
                name: "DraftResponses");

            migrationBuilder.DropTable(
                name: "MeetingSummaries");

            migrationBuilder.DropTable(
                name: "ParentDraftNotes");

            migrationBuilder.DropTable(
                name: "SharedDraftExplanations");

            migrationBuilder.DropTable(
                name: "SharedDraftRevisions");

            migrationBuilder.DropIndex(
                name: "IX_UsageRecords_DistrictId",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "UsageRecords");

            migrationBuilder.DropColumn(
                name: "FamilyDraftSharingEnabled",
                table: "Districts");

            migrationBuilder.AlterColumn<int>(
                name: "ChildProfileId",
                table: "UsageRecords",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
