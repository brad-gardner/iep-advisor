using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRosterLifecycleTeamsAndImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnnualReviewDueDate",
                table: "SchoolStudents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CaseManagerUserId",
                table: "SchoolStudents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "SchoolStudents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EtrDate",
                table: "SchoolStudents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExitReason",
                table: "SchoolStudents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExitedAt",
                table: "SchoolStudents",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalStudentId",
                table: "SchoolStudents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeLanguage",
                table: "SchoolStudents",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                defaultValue: "en");

            migrationBuilder.AddColumn<DateTime>(
                name: "IepDate",
                table: "SchoolStudents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyDisabilityText",
                table: "SchoolStudents",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReevaluationDueDate",
                table: "SchoolStudents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SchoolStudents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Active");

            // ---------------------------------------------------------------- Data conversion (T-SQL)
            // Runs after every new column exists and BEFORE IsActive is dropped / the enum columns are
            // narrowed. All statements are idempotent best-effort mappings; nothing here deletes rows.

            // Lifecycle: the old IsActive flag becomes Status (false → Archived), then the flag is dropped.
            migrationBuilder.Sql(@"
UPDATE SchoolStudents SET Status = CASE WHEN IsActive = 0 THEN 'Archived' ELSE 'Active' END;");

            // A nullable column's DEFAULT does not touch existing rows on SQL Server — backfill explicitly.
            migrationBuilder.Sql(@"
UPDATE SchoolStudents SET HomeLanguage = 'en' WHERE HomeLanguage IS NULL;");

            // Denormalized district, populated from the student's school.
            migrationBuilder.Sql(@"
UPDATE ss SET ss.DistrictId = s.DistrictId
FROM SchoolStudents ss
INNER JOIN Schools s ON s.Id = ss.SchoolId;");

            // Grade: free text → GradeLevel enum name. K/Kindergarten → K; PK/Pre-K → PK; 1–12 (with or
            // without a 'G'/'Grade ' prefix or ordinal suffix, leading zeros ok) → G#; Ungraded → Ungraded;
            // anything else → NULL.
            migrationBuilder.Sql(@"
;WITH g AS (
    SELECT Id,
           UPPER(LTRIM(RTRIM(GradeLevel))) AS Raw,
           REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(LTRIM(RTRIM(GradeLevel))), 'GRADE ', ''), 'ST', ''), 'ND', ''), 'RD', ''), 'TH', ''), 'G', '') AS Digits
    FROM SchoolStudents
    WHERE GradeLevel IS NOT NULL
)
UPDATE ss SET GradeLevel = CASE
    WHEN g.Raw IN ('K', 'KG', 'KINDERGARTEN') THEN 'K'
    WHEN g.Raw IN ('PK', 'PREK', 'PRE-K', 'PRE K', 'PRESCHOOL', 'PRE-KINDERGARTEN') THEN 'PK'
    WHEN g.Raw IN ('UNGRADED', 'UG') THEN 'Ungraded'
    WHEN TRY_CAST(LTRIM(RTRIM(g.Digits)) AS int) BETWEEN 1 AND 12 THEN 'G' + CAST(TRY_CAST(LTRIM(RTRIM(g.Digits)) AS int) AS nvarchar(2))
    ELSE NULL END
FROM SchoolStudents ss
INNER JOIN g ON g.Id = ss.Id;");

            // Disability: free text → DisabilityCategory enum name (enum names, display labels, common
            // abbreviations, Ohio wording). Unmapped non-empty text → 'Other' with the original preserved in
            // LegacyDisabilityText for a later cleanup.
            migrationBuilder.Sql(@"
;WITH d AS (
    SELECT Id,
           DisabilityCategory AS Original,
           UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(DisabilityCategory)), ' ', ''), '-', ''), '/', ''), '''', ''), '(', ''), ')', '')) AS Norm
    FROM SchoolStudents
    WHERE DisabilityCategory IS NOT NULL AND LTRIM(RTRIM(DisabilityCategory)) <> ''
),
m AS (
    SELECT Id, Original, CASE
        WHEN Norm IN ('AUTISM', 'ASD', 'AUTISMSPECTRUMDISORDER') THEN 'Autism'
        WHEN Norm IN ('DEAFBLINDNESS', 'DEAFBLIND', 'DB') THEN 'DeafBlindness'
        WHEN Norm IN ('DEAFNESS') THEN 'Deafness'
        WHEN Norm IN ('DEVELOPMENTALDELAY', 'DD', 'PRESCHOOLDEVELOPMENTALDELAY') THEN 'DevelopmentalDelay'
        WHEN Norm IN ('EMOTIONALDISTURBANCE', 'ED', 'EMOTIONALDISABILITY', 'EMOTIONALBEHAVIORALDISABILITY', 'SERIOUSEMOTIONALDISTURBANCE') THEN 'EmotionalDisturbance'
        WHEN Norm IN ('HEARINGIMPAIRMENT', 'HI', 'HEARINGIMPAIRED', 'DEAFNESSHEARINGIMPAIRMENT', 'HEARINGIMPAIRMENTINCLUDINGDEAFNESS') THEN 'HearingImpairment'
        WHEN Norm IN ('INTELLECTUALDISABILITY', 'ID', 'COGNITIVEDISABILITY', 'INTELLECTUALDISABILITIES', 'MR') THEN 'IntellectualDisability'
        WHEN Norm IN ('MULTIPLEDISABILITIES', 'MD', 'MULTIPLEDISABILITY') THEN 'MultipleDisabilities'
        WHEN Norm IN ('ORTHOPEDICIMPAIRMENT', 'OI', 'ORTHOPEDICIMPAIRED') THEN 'OrthopedicImpairment'
        WHEN Norm IN ('OTHERHEALTHIMPAIRMENT', 'OHI', 'OTHERHEALTHIMPAIRED', 'OTHERHEALTHIMPAIRMENTMAJOR', 'OTHERHEALTHIMPAIRMENTMINOR') THEN 'OtherHealthImpairment'
        WHEN Norm IN ('SPECIFICLEARNINGDISABILITY', 'SLD', 'LEARNINGDISABILITY', 'SPECIFICLEARNINGDISABILITIES') THEN 'SpecificLearningDisability'
        WHEN Norm IN ('SPEECHORLANGUAGEIMPAIRMENT', 'SLI', 'SPEECHANDLANGUAGEIMPAIRMENT', 'SPEECHLANGUAGEIMPAIRMENT', 'SPEECHIMPAIRMENT', 'LANGUAGEIMPAIRMENT') THEN 'SpeechOrLanguageImpairment'
        WHEN Norm IN ('TRAUMATICBRAININJURY', 'TBI') THEN 'TraumaticBrainInjury'
        WHEN Norm IN ('VISUALIMPAIRMENT', 'VI', 'VISUALIMPAIRMENTINCLUDINGBLINDNESS', 'BLINDNESS') THEN 'VisualImpairment'
        WHEN Norm IN ('OTHER') THEN 'Other'
        ELSE NULL END AS Mapped
    FROM d
)
UPDATE ss SET
    DisabilityCategory = COALESCE(m.Mapped, 'Other'),
    LegacyDisabilityText = CASE WHEN m.Mapped IS NULL THEN LEFT(m.Original, 200) ELSE NULL END
FROM SchoolStudents ss
INNER JOIN m ON m.Id = ss.Id;");

            // Blank/whitespace-only free text is simply NULL.
            migrationBuilder.Sql(@"
UPDATE SchoolStudents SET DisabilityCategory = NULL WHERE DisabilityCategory IS NOT NULL AND LTRIM(RTRIM(DisabilityCategory)) = '';");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "SchoolStudents");

            migrationBuilder.AlterColumn<string>(
                name: "GradeLevel",
                table: "SchoolStudents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisabilityCategory",
                table: "SchoolStudents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DistrictId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    TotalCount = table.Column<int>(type: "int", nullable: false),
                    NewCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedCount = table.Column<int>(type: "int", nullable: false),
                    UnchangedCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    CommittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentTeamMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TeamRole = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    IsLead = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentTeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentTeamMembers_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentTeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRows_ImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "OrgRoles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 4, "RelatedServiceProvider" },
                    { 5, "GeneralEducator" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudents_CaseManagerUserId",
                table: "SchoolStudents",
                column: "CaseManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudents_DistrictId_ExternalStudentId",
                table: "SchoolStudents",
                columns: new[] { "DistrictId", "ExternalStudentId" },
                unique: true,
                filter: "[ExternalStudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolStudents_DistrictId_Status",
                table: "SchoolStudents",
                columns: new[] { "DistrictId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_DistrictId_CreatedAt",
                table: "ImportBatches",
                columns: new[] { "DistrictId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_BatchId_RowNumber",
                table: "ImportRows",
                columns: new[] { "BatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeamMembers_SchoolStudentId_ActiveLead",
                table: "StudentTeamMembers",
                column: "SchoolStudentId",
                unique: true,
                filter: "[IsLead] = 1 AND [IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeamMembers_SchoolStudentId_UserId",
                table: "StudentTeamMembers",
                columns: new[] { "SchoolStudentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentTeamMembers_UserId",
                table: "StudentTeamMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolStudents_Districts_DistrictId",
                table: "SchoolStudents",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolStudents_Users_CaseManagerUserId",
                table: "SchoolStudents",
                column: "CaseManagerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchoolStudents_Districts_DistrictId",
                table: "SchoolStudents");

            migrationBuilder.DropForeignKey(
                name: "FK_SchoolStudents_Users_CaseManagerUserId",
                table: "SchoolStudents");

            migrationBuilder.DropTable(
                name: "ImportRows");

            migrationBuilder.DropTable(
                name: "StudentTeamMembers");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropIndex(
                name: "IX_SchoolStudents_CaseManagerUserId",
                table: "SchoolStudents");

            migrationBuilder.DropIndex(
                name: "IX_SchoolStudents_DistrictId_ExternalStudentId",
                table: "SchoolStudents");

            migrationBuilder.DropIndex(
                name: "IX_SchoolStudents_DistrictId_Status",
                table: "SchoolStudents");

            migrationBuilder.DeleteData(
                table: "OrgRoles",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "OrgRoles",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DropColumn(
                name: "AnnualReviewDueDate",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "CaseManagerUserId",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "EtrDate",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "ExitReason",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "ExitedAt",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "ExternalStudentId",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "HomeLanguage",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "IepDate",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "LegacyDisabilityText",
                table: "SchoolStudents");

            migrationBuilder.DropColumn(
                name: "ReevaluationDueDate",
                table: "SchoolStudents");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "SchoolStudents",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(@"
UPDATE SchoolStudents SET IsActive = CASE WHEN Status = 'Active' THEN 1 ELSE 0 END;");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SchoolStudents");

            migrationBuilder.AlterColumn<string>(
                name: "GradeLevel",
                table: "SchoolStudents",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DisabilityCategory",
                table: "SchoolStudents",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }
    }
}
