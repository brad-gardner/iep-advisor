using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPilotGatesPhase12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DraftAcknowledgements_SharedDraftRevisions_SharedDraftRevisionId",
                table: "DraftAcknowledgements");

            migrationBuilder.DropForeignKey(
                name: "FK_DraftResponses_SharedDraftRevisions_SharedDraftRevisionId",
                table: "DraftResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_ParentDraftNotes_SharedDraftRevisions_SharedDraftRevisionId",
                table: "ParentDraftNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_SharedDraftExplanations_SharedDraftRevisions_SharedDraftRevisionId",
                table: "SharedDraftExplanations");

            migrationBuilder.DropForeignKey(
                name: "FK_SharedDraftRevisions_DocumentInstances_DocumentInstanceId",
                table: "SharedDraftRevisions");

            migrationBuilder.AddColumn<string>(
                name: "Hash",
                table: "AccessAuditLogs",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrevHash",
                table: "AccessAuditLogs",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditIntegrityRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowsChecked = table.Column<int>(type: "int", nullable: false),
                    FirstBrokenId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditIntegrityRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboundEmails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ToEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HtmlBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TextBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AttachmentsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboundEmails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingAuditEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActionValue = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceId = table.Column<int>(type: "int", nullable: false),
                    RecipientUserId = table.Column<int>(type: "int", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EnqueuedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditIntegrityRuns_StartedAt",
                table: "AuditIntegrityRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboundEmails_Status_CreatedAt",
                table: "OutboundEmails",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboundEmails_Status_NextAttemptAt",
                table: "OutboundEmails",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PendingAuditEvents_Id",
                table: "PendingAuditEvents",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DraftAcknowledgements_SharedDraftRevisions_SharedDraftRevisionId",
                table: "DraftAcknowledgements",
                column: "SharedDraftRevisionId",
                principalTable: "SharedDraftRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DraftResponses_SharedDraftRevisions_SharedDraftRevisionId",
                table: "DraftResponses",
                column: "SharedDraftRevisionId",
                principalTable: "SharedDraftRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentDraftNotes_SharedDraftRevisions_SharedDraftRevisionId",
                table: "ParentDraftNotes",
                column: "SharedDraftRevisionId",
                principalTable: "SharedDraftRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SharedDraftExplanations_SharedDraftRevisions_SharedDraftRevisionId",
                table: "SharedDraftExplanations",
                column: "SharedDraftRevisionId",
                principalTable: "SharedDraftRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SharedDraftRevisions_DocumentInstances_DocumentInstanceId",
                table: "SharedDraftRevisions",
                column: "DocumentInstanceId",
                principalTable: "DocumentInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // SQL Server-only immutability triggers (pilot-gates plan, phase 1, decision 1). SQLite
            // (used by every Services.Tests context) has no equivalent, so tests skip this via
            // Database.IsSqlServer() — the C# ImmutableVersionInterceptor remains the enforcement
            // there. IF NOT EXISTS makes this safe to re-run; CREATE TRIGGER must be the only
            // statement in its batch, hence the EXEC('...') dynamic SQL wrapper. Each trigger rejects
            // every UPDATE/DELETE except a narrow, named carve-out for a column set the application
            // genuinely needs to keep changing after the row is written; every other column change,
            // and any DELETE, is rejected. A no-op UPDATE/DELETE (WHERE clause matching zero rows) is
            // allowed through untouched (both `inserted`/`deleted` are empty).
            //
            // The five FK changes just above (Cascade -> Restrict, on every FK touching
            // SharedDraftRevisions in either direction) are a required prerequisite, not an unrelated
            // cleanup: SQL Server refuses to create an INSTEAD OF UPDATE/DELETE trigger on a table
            // with ANY cascading FK touching it (verified against a real SQL Server instance while
            // authoring this migration — error 2113, twice, until every one of the five was found).
            // A SharedDraftRevision is never actually deleted by application code (only
            // superseded/withdrawn via Status), so four of the five change no real runtime behavior;
            // the fifth (DocumentInstance -> SharedDraftRevisions) is handled explicitly in
            // DocumentInstanceService.DeleteAsync (a DbUpdateException now returns a friendly refusal
            // instead of silently cascading away a family's shared draft history).
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql(
                    """
                    IF NOT EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_AccessAuditLogs_Immutable')
                    EXEC('CREATE TRIGGER TR_AccessAuditLogs_Immutable ON AccessAuditLogs
                    INSTEAD OF UPDATE, DELETE
                    AS
                    BEGIN
                        SET NOCOUNT ON;

                        IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
                        BEGIN
                            RAISERROR(''AccessAuditLog records are immutable.'', 16, 1);
                            RETURN;
                        END

                        IF NOT EXISTS (SELECT 1 FROM inserted)
                            RETURN;

                        -- The ONE permitted post-insert update: AccessAuditLogWorker''s hash-chain
                        -- step setting Hash/PrevHash from NULL once the row''s Id is known. Every
                        -- other column, and re-hashing an already-hashed row, is rejected.
                        IF EXISTS (
                            SELECT 1
                            FROM deleted d
                            JOIN inserted i ON i.Id = d.Id
                            WHERE d.Hash IS NOT NULL
                               OR i.Hash IS NULL
                               OR d.Action <> i.Action
                               OR d.ActorUserId <> i.ActorUserId
                               OR d.ResourceType <> i.ResourceType
                               OR d.ResourceId <> i.ResourceId
                               OR ISNULL(d.RecipientUserId, -1) <> ISNULL(i.RecipientUserId, -1)
                               OR d.CreatedAt <> i.CreatedAt
                        )
                        BEGIN
                            RAISERROR(''AccessAuditLog records are immutable except the one-time Hash/PrevHash backfill.'', 16, 1);
                            RETURN;
                        END

                        UPDATE t SET Hash = i.Hash, PrevHash = i.PrevHash
                        FROM AccessAuditLogs t
                        JOIN inserted i ON i.Id = t.Id;
                    END')
                    """);

                migrationBuilder.Sql(
                    """
                    IF NOT EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_AuthoredDocumentVersions_Immutable')
                    EXEC('CREATE TRIGGER TR_AuthoredDocumentVersions_Immutable ON AuthoredDocumentVersions
                    INSTEAD OF UPDATE, DELETE
                    AS
                    BEGIN
                        SET NOCOUNT ON;

                        IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
                        BEGIN
                            RAISERROR(''AuthoredDocumentVersion records are immutable.'', 16, 1);
                            RETURN;
                        END

                        IF NOT EXISTS (SELECT 1 FROM inserted)
                            RETURN;

                        -- Permitted post-finalize update: SignatureStatus (plan 7, decision 4) plus
                        -- the audit stamp. Every other column is frozen at finalize.
                        IF EXISTS (
                            SELECT 1
                            FROM deleted d
                            JOIN inserted i ON i.Id = d.Id
                            WHERE d.SchoolStudentId <> i.SchoolStudentId
                               OR d.DocumentTypeId <> i.DocumentTypeId
                               OR d.DocumentTemplateVersionId <> i.DocumentTemplateVersionId
                               OR d.VersionNumber <> i.VersionNumber
                               OR d.ValuesJson <> i.ValuesJson
                               OR d.FinalizedByUserId <> i.FinalizedByUserId
                               OR d.FinalizedAt <> i.FinalizedAt
                               OR ISNULL(d.AmendsVersionId, -1) <> ISNULL(i.AmendsVersionId, -1)
                               OR ISNULL(d.AmendmentReason, N'''') <> ISNULL(i.AmendmentReason, N'''')
                               OR ISNULL(d.EffectiveDate, ''1900-01-01'') <> ISNULL(i.EffectiveDate, ''1900-01-01'')
                               OR d.CreatedAt <> i.CreatedAt
                               OR ISNULL(d.CreatedById, -1) <> ISNULL(i.CreatedById, -1)
                        )
                        BEGIN
                            RAISERROR(''AuthoredDocumentVersion records are immutable except SignatureStatus.'', 16, 1);
                            RETURN;
                        END

                        UPDATE t SET SignatureStatus = i.SignatureStatus, UpdatedAt = i.UpdatedAt, UpdatedById = i.UpdatedById
                        FROM AuthoredDocumentVersions t
                        JOIN inserted i ON i.Id = t.Id;
                    END')
                    """);

                migrationBuilder.Sql(
                    """
                    IF NOT EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_IepVersions_Immutable')
                    EXEC('CREATE TRIGGER TR_IepVersions_Immutable ON IepVersions
                    INSTEAD OF UPDATE, DELETE
                    AS
                    BEGIN
                        SET NOCOUNT ON;

                        IF NOT EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted)
                            RETURN;

                        RAISERROR(''IepVersion records are immutable.'', 16, 1);
                    END')
                    """);

                migrationBuilder.Sql(
                    """
                    IF NOT EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_SharedDraftRevisions_Immutable')
                    EXEC('CREATE TRIGGER TR_SharedDraftRevisions_Immutable ON SharedDraftRevisions
                    INSTEAD OF UPDATE, DELETE
                    AS
                    BEGIN
                        SET NOCOUNT ON;

                        IF EXISTS (SELECT 1 FROM deleted) AND NOT EXISTS (SELECT 1 FROM inserted)
                        BEGIN
                            RAISERROR(''SharedDraftRevision records are immutable.'', 16, 1);
                            RETURN;
                        END

                        IF NOT EXISTS (SELECT 1 FROM inserted)
                            RETURN;

                        -- Permitted post-share updates: supersede (Status only, when a later revision
                        -- is shared) and withdraw (Status/WithdrawnAt/WithdrawnByUserId) plus the
                        -- audit stamp. Every other column is frozen at share time.
                        IF EXISTS (
                            SELECT 1
                            FROM deleted d
                            JOIN inserted i ON i.Id = d.Id
                            WHERE d.DocumentInstanceId <> i.DocumentInstanceId
                               OR d.RevisionNumber <> i.RevisionNumber
                               OR d.ValuesJson <> i.ValuesJson
                               OR d.DocumentTemplateVersionId <> i.DocumentTemplateVersionId
                               OR d.SharedByUserId <> i.SharedByUserId
                               OR d.SharedAt <> i.SharedAt
                               OR ISNULL(d.Message, N'''') <> ISNULL(i.Message, N'''')
                               OR ISNULL(d.ChangeSummaryJson, N'''') <> ISNULL(i.ChangeSummaryJson, N'''')
                               OR d.CreatedAt <> i.CreatedAt
                               OR ISNULL(d.CreatedById, -1) <> ISNULL(i.CreatedById, -1)
                        )
                        BEGIN
                            RAISERROR(''SharedDraftRevision records are immutable except Status/WithdrawnAt/WithdrawnByUserId.'', 16, 1);
                            RETURN;
                        END

                        UPDATE t SET Status = i.Status, WithdrawnAt = i.WithdrawnAt, WithdrawnByUserId = i.WithdrawnByUserId,
                                     UpdatedAt = i.UpdatedAt, UpdatedById = i.UpdatedById
                        FROM SharedDraftRevisions t
                        JOIN inserted i ON i.Id = t.Id;
                    END')
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_AccessAuditLogs_Immutable') DROP TRIGGER TR_AccessAuditLogs_Immutable;");
                migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_AuthoredDocumentVersions_Immutable') DROP TRIGGER TR_AuthoredDocumentVersions_Immutable;");
                migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_IepVersions_Immutable') DROP TRIGGER TR_IepVersions_Immutable;");
                migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.triggers WHERE name = 'TR_SharedDraftRevisions_Immutable') DROP TRIGGER TR_SharedDraftRevisions_Immutable;");
            }

            migrationBuilder.DropForeignKey(
                name: "FK_DraftAcknowledgements_SharedDraftRevisions_SharedDraftRevisionId",
                table: "DraftAcknowledgements");

            migrationBuilder.DropForeignKey(
                name: "FK_DraftResponses_SharedDraftRevisions_SharedDraftRevisionId",
                table: "DraftResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_ParentDraftNotes_SharedDraftRevisions_SharedDraftRevisionId",
                table: "ParentDraftNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_SharedDraftExplanations_SharedDraftRevisions_SharedDraftRevisionId",
                table: "SharedDraftExplanations");

            migrationBuilder.DropForeignKey(
                name: "FK_SharedDraftRevisions_DocumentInstances_DocumentInstanceId",
                table: "SharedDraftRevisions");

            migrationBuilder.DropTable(
                name: "AuditIntegrityRuns");

            migrationBuilder.DropTable(
                name: "OutboundEmails");

            migrationBuilder.DropTable(
                name: "PendingAuditEvents");

            migrationBuilder.DropColumn(
                name: "Hash",
                table: "AccessAuditLogs");

            migrationBuilder.DropColumn(
                name: "PrevHash",
                table: "AccessAuditLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_DraftAcknowledgements_SharedDraftRevisions_SharedDraftRevisionId",
                table: "DraftAcknowledgements",
                column: "SharedDraftRevisionId",
                principalTable: "SharedDraftRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DraftResponses_SharedDraftRevisions_SharedDraftRevisionId",
                table: "DraftResponses",
                column: "SharedDraftRevisionId",
                principalTable: "SharedDraftRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ParentDraftNotes_SharedDraftRevisions_SharedDraftRevisionId",
                table: "ParentDraftNotes",
                column: "SharedDraftRevisionId",
                principalTable: "SharedDraftRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SharedDraftExplanations_SharedDraftRevisions_SharedDraftRevisionId",
                table: "SharedDraftExplanations",
                column: "SharedDraftRevisionId",
                principalTable: "SharedDraftRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SharedDraftRevisions_DocumentInstances_DocumentInstanceId",
                table: "SharedDraftRevisions",
                column: "DocumentInstanceId",
                principalTable: "DocumentInstances",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
