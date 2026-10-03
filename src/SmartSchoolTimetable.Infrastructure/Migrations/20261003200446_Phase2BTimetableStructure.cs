using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2BTimetableStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BellSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Tone = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    BreakBell = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BellSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shifts_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkingWeek",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    DaysMask = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekStartDay = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingWeek", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LessonPeriods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    StartBell = table.Column<bool>(type: "INTEGER", nullable: false),
                    EndBell = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShiftId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonPeriods_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonPeriods_ShiftId_Position",
                table: "LessonPeriods",
                columns: new[] { "ShiftId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_AcademicYearId_DisplayOrder",
                table: "Shifts",
                columns: new[] { "AcademicYearId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_AcademicYearId_NormalizedName",
                table: "Shifts",
                columns: new[] { "AcademicYearId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BellSettings");

            migrationBuilder.DropTable(
                name: "LessonPeriods");

            migrationBuilder.DropTable(
                name: "WorkingWeek");

            migrationBuilder.DropTable(
                name: "Shifts");
        }
    }
}
