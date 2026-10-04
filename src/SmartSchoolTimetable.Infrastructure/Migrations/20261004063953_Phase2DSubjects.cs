using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2DSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ColorIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    DistributionEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    SpreadAcrossDays = table.Column<bool>(type: "INTEGER", nullable: false),
                    Heavy = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresDoublePeriod = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.CheckConstraint("CK_Subjects_ColorIndex", "\"ColorIndex\" BETWEEN 1 AND 10");
                    table.CheckConstraint("CK_Subjects_Priority", "\"Priority\" BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "SubjectBlockedPeriods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    LessonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    SubjectId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectBlockedPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubjectBlockedPeriods_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubjectBlockedPeriods_SubjectId_Day_LessonNumber",
                table: "SubjectBlockedPeriods",
                columns: new[] { "SubjectId", "Day", "LessonNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_IsArchived",
                table: "Subjects",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_NormalizedName",
                table: "Subjects",
                column: "NormalizedName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubjectBlockedPeriods");

            migrationBuilder.DropTable(
                name: "Subjects");
        }
    }
}
