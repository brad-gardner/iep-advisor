using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IepAssistant.Domain.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffInvite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffInvites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DistrictId = table.Column<int>(type: "int", nullable: false),
                    SchoolId = table.Column<int>(type: "int", nullable: true),
                    OrgRoleId = table.Column<int>(type: "int", nullable: false),
                    InviteToken = table.Column<string>(type: "nvarchar(88)", maxLength: 88, nullable: true),
                    InviteExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AcceptedByUserId = table.Column<int>(type: "int", nullable: true),
                    InvitedByUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<int>(type: "int", nullable: true),
                    UpdatedById = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StaffInvites_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffInvites_OrgRoles_OrgRoleId",
                        column: x => x.OrgRoleId,
                        principalTable: "OrgRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffInvites_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvites_DistrictId",
                table: "StaffInvites",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvites_Email",
                table: "StaffInvites",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvites_InviteToken",
                table: "StaffInvites",
                column: "InviteToken");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvites_OrgRoleId",
                table: "StaffInvites",
                column: "OrgRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_StaffInvites_SchoolId",
                table: "StaffInvites",
                column: "SchoolId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffInvites");
        }
    }
}
