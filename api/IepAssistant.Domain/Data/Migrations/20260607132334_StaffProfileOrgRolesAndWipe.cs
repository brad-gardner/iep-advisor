using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffProfileOrgRolesAndWipe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // =====================================================================================
            // PART 1 — DEV ORG-DATA WIPE (user decision; no production data exists).
            //
            // Raw SQL, child-first per the actual FK graph, executed BEFORE any schema change so the
            // TeacherProfiles -> StaffProfiles rename (which adds required DistrictId/OrgRoleId columns)
            // lands on an empty table. This bypasses the EF-only ImmutableVersionInterceptor — acceptable
            // here because it is dev data and there is no prod data to protect (see plan "State Lifecycle
            // Risks"). Precedent for raw SQL in a migration: 20260602130126_ConvertUserRoleToEnum.
            //
            // Order rationale (delete dependents before principals):
            //   StudentWorkspaceEntries  -> StudentWorkspaces            (workspace is per student-user)
            //   StudentProfiles          (FK SchoolStudent/ChildProfile)
            //   StudentInvites           (FK SchoolStudent/ChildProfile)
            //   IepVersion child tables  -> IepVersionPdfs -> IepVersions (FK SchoolStudent, Restrict)
            //   IepDraft child tables    -> IepDrafts                      (FK SchoolStudent)
            //   SchoolStudentAccesses    (FK SchoolStudent)
            //   ChildLinks               (FK SchoolStudent)
            //   SchoolStudents           (FK School)
            //   StaffProfiles/TeacherProfiles (FK School/District/User)
            //   Schools                  (FK District)
            //   Districts
            // Finally: reset every Educator/Student user back to Parent.
            // =====================================================================================
            migrationBuilder.Sql(@"
                DELETE FROM StudentWorkspaceEntries;
                DELETE FROM StudentWorkspaces;
                DELETE FROM StudentProfiles;
                DELETE FROM StudentInvites;

                DELETE FROM IepVersionTransitionItems;
                DELETE FROM IepVersionServiceLines;
                DELETE FROM IepVersionGoals;
                DELETE FROM IepVersionAccommodations;
                DELETE FROM IepVersionSections;
                DELETE FROM IepVersionPdfs;
                DELETE FROM IepVersions;

                DELETE FROM IepDraftTransitionItems;
                DELETE FROM IepDraftServiceLines;
                DELETE FROM IepDraftGoals;
                DELETE FROM IepDraftAccommodations;
                DELETE FROM IepDraftSections;
                DELETE FROM IepDrafts;

                DELETE FROM SchoolStudentAccesses;
                DELETE FROM ChildLinks;
                DELETE FROM SchoolStudents;
                DELETE FROM TeacherProfiles;
                DELETE FROM Schools;
                DELETE FROM Districts;

                UPDATE Users SET Role = 'Parent' WHERE Role IN ('Educator', 'Student');
            ");

            // =====================================================================================
            // PART 2 — OrgRoles lookup table (seeded 1=DistrictAdmin, 2=SchoolAdmin, 3=Teacher).
            // =====================================================================================
            migrationBuilder.CreateTable(
                name: "OrgRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgRoles", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "OrgRoles",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "DistrictAdmin" },
                    { 2, "SchoolAdmin" },
                    { 3, "Teacher" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgRoles_Name",
                table: "OrgRoles",
                column: "Name",
                unique: true);

            // =====================================================================================
            // PART 3 — Rename TeacherProfiles -> StaffProfiles in place (clean rename, not drop/create),
            // then evolve the schema: new required DistrictId/OrgRoleId, new IsActive, nullable SchoolId.
            // Table is empty (wiped above), so adding the NOT NULL columns is safe without defaults.
            // =====================================================================================

            // 3a. Drop the old FK/PK/indexes so the rename leaves no stale TeacherProfiles_* objects.
            migrationBuilder.DropForeignKey(
                name: "FK_TeacherProfiles_Schools_SchoolId",
                table: "TeacherProfiles");
            migrationBuilder.DropForeignKey(
                name: "FK_TeacherProfiles_Users_UserId",
                table: "TeacherProfiles");
            migrationBuilder.DropPrimaryKey(
                name: "PK_TeacherProfiles",
                table: "TeacherProfiles");
            migrationBuilder.DropIndex(
                name: "IX_TeacherProfiles_SchoolId",
                table: "TeacherProfiles");
            migrationBuilder.DropIndex(
                name: "IX_TeacherProfiles_UserId",
                table: "TeacherProfiles");

            // 3b. Rename the table.
            migrationBuilder.RenameTable(
                name: "TeacherProfiles",
                newName: "StaffProfiles");

            // 3c. Make SchoolId nullable (null = DistrictAdmin not bound to a single school).
            migrationBuilder.AlterColumn<int>(
                name: "SchoolId",
                table: "StaffProfiles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            // 3d. Add the new columns. Table is empty so NOT NULL needs no default backfill.
            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "StaffProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.AddColumn<int>(
                name: "OrgRoleId",
                table: "StaffProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "StaffProfiles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // 3e. Re-create the PK and the new index/FK set under StaffProfiles_* names.
            migrationBuilder.AddPrimaryKey(
                name: "PK_StaffProfiles",
                table: "StaffProfiles",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_DistrictId",
                table: "StaffProfiles",
                column: "DistrictId");
            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_OrgRoleId",
                table: "StaffProfiles",
                column: "OrgRoleId");
            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_SchoolId",
                table: "StaffProfiles",
                column: "SchoolId");
            migrationBuilder.CreateIndex(
                name: "IX_StaffProfiles_UserId",
                table: "StaffProfiles",
                column: "UserId");

            // All FKs are Restrict: District cascades to School, so a Cascade from School (or District)
            // into StaffProfiles would create a second cascade path and trip SQL Server's
            // multiple-cascade-paths error. Restrict keeps the FK graph conflict-free; school/staff
            // deactivation is a soft-delete handled at the service layer (P3/P4).
            migrationBuilder.AddForeignKey(
                name: "FK_StaffProfiles_Districts_DistrictId",
                table: "StaffProfiles",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_StaffProfiles_OrgRoles_OrgRoleId",
                table: "StaffProfiles",
                column: "OrgRoleId",
                principalTable: "OrgRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_StaffProfiles_Schools_SchoolId",
                table: "StaffProfiles",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddForeignKey(
                name: "FK_StaffProfiles_Users_UserId",
                table: "StaffProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: the PART 1 dev-org-data wipe in Up() is IRREVERSIBLE — the deleted org/student/IEP
            // rows and the Educator/Student -> Parent role resets cannot be restored here. Down() only
            // reverses the schema (StaffProfiles -> TeacherProfiles, drop OrgRoles); it does not and
            // cannot recover the wiped data.

            migrationBuilder.DropForeignKey(
                name: "FK_StaffProfiles_Districts_DistrictId",
                table: "StaffProfiles");
            migrationBuilder.DropForeignKey(
                name: "FK_StaffProfiles_OrgRoles_OrgRoleId",
                table: "StaffProfiles");
            migrationBuilder.DropForeignKey(
                name: "FK_StaffProfiles_Schools_SchoolId",
                table: "StaffProfiles");
            migrationBuilder.DropForeignKey(
                name: "FK_StaffProfiles_Users_UserId",
                table: "StaffProfiles");
            migrationBuilder.DropPrimaryKey(
                name: "PK_StaffProfiles",
                table: "StaffProfiles");
            migrationBuilder.DropIndex(
                name: "IX_StaffProfiles_DistrictId",
                table: "StaffProfiles");
            migrationBuilder.DropIndex(
                name: "IX_StaffProfiles_OrgRoleId",
                table: "StaffProfiles");
            migrationBuilder.DropIndex(
                name: "IX_StaffProfiles_SchoolId",
                table: "StaffProfiles");
            migrationBuilder.DropIndex(
                name: "IX_StaffProfiles_UserId",
                table: "StaffProfiles");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "StaffProfiles");
            migrationBuilder.DropColumn(
                name: "OrgRoleId",
                table: "StaffProfiles");
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "StaffProfiles");

            // Restore SchoolId to NOT NULL (the table is empty by this point in any realistic rollback).
            migrationBuilder.AlterColumn<int>(
                name: "SchoolId",
                table: "StaffProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.RenameTable(
                name: "StaffProfiles",
                newName: "TeacherProfiles");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TeacherProfiles",
                table: "TeacherProfiles",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherProfiles_SchoolId",
                table: "TeacherProfiles",
                column: "SchoolId");
            migrationBuilder.CreateIndex(
                name: "IX_TeacherProfiles_UserId",
                table: "TeacherProfiles",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeacherProfiles_Schools_SchoolId",
                table: "TeacherProfiles",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
            migrationBuilder.AddForeignKey(
                name: "FK_TeacherProfiles_Users_UserId",
                table: "TeacherProfiles",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "OrgRoles");
        }
    }
}
