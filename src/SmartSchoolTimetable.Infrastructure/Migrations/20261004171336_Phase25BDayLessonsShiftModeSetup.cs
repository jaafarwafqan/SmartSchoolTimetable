using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase25BDayLessonsShiftModeSetup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Shifts",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Other"); // existing shifts are custom shifts (ShiftKind.Other)

            migrationBuilder.CreateTable(
                name: "SetupProgress",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    CurrentStep = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedMask = table.Column<int>(type: "INTEGER", nullable: false),
                    SkippedMask = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFinished = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SetupProgress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftDayLessons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    Lessons = table.Column<int>(type: "INTEGER", nullable: false),
                    ShiftId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftDayLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftDayLessons_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftDayLessons_ShiftId_Day",
                table: "ShiftDayLessons",
                columns: new[] { "ShiftId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SetupProgress");

            migrationBuilder.DropTable(
                name: "ShiftDayLessons");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Shifts");
        }
    }
}
