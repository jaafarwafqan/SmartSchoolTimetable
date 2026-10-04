using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2ETeachers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Teachers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    NormalizedFullName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NormalizedShortName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    OffDaysMask = table.Column<int>(type: "INTEGER", nullable: false),
                    FullyReleased = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReleaseReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ReleaseFrom = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ReleaseTo = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    MaxLessonsPerDay = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxLessonsPerWeek = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teachers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeacherBlockedPeriods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    LessonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TeacherId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherBlockedPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherBlockedPeriods_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherBlockedPeriods_TeacherId_Day_LessonNumber",
                table: "TeacherBlockedPeriods",
                columns: new[] { "TeacherId", "Day", "LessonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_IsArchived",
                table: "Teachers",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_NormalizedFullName",
                table: "Teachers",
                column: "NormalizedFullName");

            migrationBuilder.CreateIndex(
                name: "IX_Teachers_NormalizedShortName",
                table: "Teachers",
                column: "NormalizedShortName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherBlockedPeriods");

            migrationBuilder.DropTable(
                name: "Teachers");
        }
    }
}
