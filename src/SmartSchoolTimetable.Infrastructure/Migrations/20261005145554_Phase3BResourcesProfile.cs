using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3BResourcesProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RequiredResourceId",
                table: "Subjects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Capacity = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.CheckConstraint("CK_Resources_Capacity", "\"Capacity\" BETWEEN 1 AND 20");
                    table.CheckConstraint("CK_Resources_Kind", "\"Kind\" BETWEEN 1 AND 4");
                });

            migrationBuilder.CreateTable(
                name: "SchedulingProfile",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    ProfileVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingProfile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeacherSpecializations",
                columns: table => new
                {
                    SubjectId = table.Column<long>(type: "INTEGER", nullable: false),
                    TeacherId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherSpecializations", x => new { x.TeacherId, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_TeacherSpecializations_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeacherSpecializations_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchedulingProfileRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Weight = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulingProfileRules", x => x.Id);
                    table.CheckConstraint("CK_SchedulingProfileRules_Weight", "\"Weight\" BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_SchedulingProfileRules_SchedulingProfile_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "SchedulingProfile",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_RequiredResourceId",
                table: "Subjects",
                column: "RequiredResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_IsArchived",
                table: "Resources",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_NormalizedName",
                table: "Resources",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulingProfileRules_ProfileId_Key",
                table: "SchedulingProfileRules",
                columns: new[] { "ProfileId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherSpecializations_SubjectId",
                table: "TeacherSpecializations",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_Resources_RequiredResourceId",
                table: "Subjects",
                column: "RequiredResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_Resources_RequiredResourceId",
                table: "Subjects");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropTable(
                name: "SchedulingProfileRules");

            migrationBuilder.DropTable(
                name: "TeacherSpecializations");

            migrationBuilder.DropTable(
                name: "SchedulingProfile");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_RequiredResourceId",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "RequiredResourceId",
                table: "Subjects");
        }
    }
}
