using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2ASchoolProfileAndAcademicYears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcademicYears",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    NormalizedLabel = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    CurrentTermId = table.Column<long>(type: "INTEGER", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolProfile",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SchoolType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StudyType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PrincipalName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    ScheduleOfficerName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NumeralSystem = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CalendarDisplay = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LogoStoredFileName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    LogoContentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LogoSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    LogoUploadedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    StampStoredFileName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StampContentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    StampSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    StampUploadedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolProfile", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Terms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    AcademicYearId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Terms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Terms_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_IsCurrent",
                table: "AcademicYears",
                column: "IsCurrent",
                unique: true,
                filter: "\"IsCurrent\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicYears_NormalizedLabel",
                table: "AcademicYears",
                column: "NormalizedLabel",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Terms_AcademicYearId_NormalizedName",
                table: "Terms",
                columns: new[] { "AcademicYearId", "NormalizedName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchoolProfile");

            migrationBuilder.DropTable(
                name: "Terms");

            migrationBuilder.DropTable(
                name: "AcademicYears");
        }
    }
}
