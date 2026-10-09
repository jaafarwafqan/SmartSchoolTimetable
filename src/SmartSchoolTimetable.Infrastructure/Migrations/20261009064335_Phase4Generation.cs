using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4Generation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GenerationRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TimeLimitSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Seed = table.Column<int>(type: "INTEGER", nullable: false),
                    Workers = table.Column<int>(type: "INTEGER", nullable: false),
                    Deterministic = table.Column<bool>(type: "INTEGER", nullable: false),
                    SolverVersion = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SolverParameters = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ProfileVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ElapsedSeconds = table.Column<double>(type: "REAL", nullable: true),
                    Objective = table.Column<long>(type: "INTEGER", nullable: true),
                    Bound = table.Column<long>(type: "INTEGER", nullable: true),
                    Optimal = table.Column<bool>(type: "INTEGER", nullable: false),
                    Improvements = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSolutionSeconds = table.Column<double>(type: "REAL", nullable: true),
                    LessonsPlaced = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoreJson = table.Column<string>(type: "TEXT", nullable: true),
                    DiagnosticsJson = table.Column<string>(type: "TEXT", nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    TimetableVersionId = table.Column<long>(type: "INTEGER", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationRuns", x => x.Id);
                    table.CheckConstraint("CK_GenerationRuns_Status", "\"Status\" BETWEEN 1 AND 9");
                    table.ForeignKey(
                        name: "FK_GenerationRuns_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimetableVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<long>(type: "INTEGER", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    GenerationRunId = table.Column<long>(type: "INTEGER", nullable: true),
                    ParentVersionId = table.Column<long>(type: "INTEGER", nullable: true),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    InputJson = table.Column<string>(type: "TEXT", nullable: false),
                    ScoreJson = table.Column<string>(type: "TEXT", nullable: true),
                    Score = table.Column<long>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableVersions", x => x.Id);
                    table.CheckConstraint("CK_TimetableVersions_Source", "\"Source\" BETWEEN 1 AND 2");
                    table.ForeignKey(
                        name: "FK_TimetableVersions_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableVersions_GenerationRuns_GenerationRunId",
                        column: x => x.GenerationRunId,
                        principalTable: "GenerationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetableVersions_TimetableVersions_ParentVersionId",
                        column: x => x.ParentVersionId,
                        principalTable: "TimetableVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimetableLessons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectionId = table.Column<long>(type: "INTEGER", nullable: false),
                    CurriculumEntryId = table.Column<long>(type: "INTEGER", nullable: false),
                    TeacherId = table.Column<long>(type: "INTEGER", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    LessonNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TimetableVersionId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetableLessons", x => x.Id);
                    table.CheckConstraint("CK_TimetableLessons_Slot", "\"Day\" BETWEEN 1 AND 7 AND \"LessonNumber\" BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_TimetableLessons_TimetableVersions_TimetableVersionId",
                        column: x => x.TimetableVersionId,
                        principalTable: "TimetableVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_AcademicYearId_Id",
                table: "GenerationRuns",
                columns: new[] { "AcademicYearId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationRuns_Status",
                table: "GenerationRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableLessons_TimetableVersionId_SectionId",
                table: "TimetableLessons",
                columns: new[] { "TimetableVersionId", "SectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableLessons_TimetableVersionId_TeacherId",
                table: "TimetableLessons",
                columns: new[] { "TimetableVersionId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetableVersions_AcademicYearId_Number",
                table: "TimetableVersions",
                columns: new[] { "AcademicYearId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimetableVersions_Approved_Year",
                table: "TimetableVersions",
                column: "AcademicYearId",
                unique: true,
                filter: "\"IsApproved\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableVersions_GenerationRunId",
                table: "TimetableVersions",
                column: "GenerationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetableVersions_ParentVersionId",
                table: "TimetableVersions",
                column: "ParentVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TimetableLessons");

            migrationBuilder.DropTable(
                name: "TimetableVersions");

            migrationBuilder.DropTable(
                name: "GenerationRuns");
        }
    }
}
