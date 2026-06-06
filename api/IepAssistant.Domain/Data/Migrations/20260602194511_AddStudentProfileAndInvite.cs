using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentProfileAndInvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InviteEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InviteToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InviteExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvitedByUserId = table.Column<int>(type: "int", nullable: false),
                    ChildProfileId = table.Column<int>(type: "int", nullable: true),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentInvites_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentInvites_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StudentProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StateCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ConsentAcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ChildProfileId = table.Column<int>(type: "int", nullable: true),
                    SchoolStudentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentProfiles_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentProfiles_SchoolStudents_SchoolStudentId",
                        column: x => x.SchoolStudentId,
                        principalTable: "SchoolStudents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StudentInvites_ChildProfileId",
                table: "StudentInvites",
                column: "ChildProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentInvites_InviteEmail",
                table: "StudentInvites",
                column: "InviteEmail");

            migrationBuilder.CreateIndex(
                name: "IX_StudentInvites_InviteToken",
                table: "StudentInvites",
                column: "InviteToken");

            migrationBuilder.CreateIndex(
                name: "IX_StudentInvites_SchoolStudentId",
                table: "StudentInvites",
                column: "SchoolStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_ChildProfileId",
                table: "StudentProfiles",
                column: "ChildProfileId",
                unique: true,
                filter: "[ChildProfileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_SchoolStudentId",
                table: "StudentProfiles",
                column: "SchoolStudentId",
                unique: true,
                filter: "[SchoolStudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_UserId",
                table: "StudentProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentInvites");

            migrationBuilder.DropTable(
                name: "StudentProfiles");
        }
    }
}
