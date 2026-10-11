using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5Settings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppPreferences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DefaultSemester = table.Column<int>(type: "INTEGER", nullable: true),
                    SectionPaper = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    SectionOrientation = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    TeacherPaper = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    TeacherOrientation = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SchoolPaper = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    SchoolOrientation = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    PrintFit = table.Column<bool>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPreferences", x => x.Id);
                    table.CheckConstraint("CK_AppPreferences_Semester", "\"DefaultSemester\" IS NULL OR \"DefaultSemester\" IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "BackupSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Folder = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AutoBackupEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    KeepLast = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAutoBackupAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastAppVersion = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupSettings", x => x.Id);
                    table.CheckConstraint("CK_BackupSettings_KeepLast", "\"KeepLast\" IN (0, 3, 7, 14, 30)");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPreferences");

            migrationBuilder.DropTable(
                name: "BackupSettings");
        }
    }
}
