using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4SessionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionPlans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AcademicYearId = table.Column<long>(type: "INTEGER", nullable: false),
                    ShiftId = table.Column<long>(type: "INTEGER", nullable: false),
                    System = table.Column<int>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionPlans_AcademicYears_AcademicYearId",
                        column: x => x.AcademicYearId,
                        principalTable: "AcademicYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionPlans_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionDays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Term = table.Column<int>(type: "INTEGER", nullable: false),
                    Day = table.Column<int>(type: "INTEGER", nullable: false),
                    Session = table.Column<int>(type: "INTEGER", nullable: false),
                    SessionPlanId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionDays_SessionPlans_SessionPlanId",
                        column: x => x.SessionPlanId,
                        principalTable: "SessionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionPeriods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Session = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "TEXT", nullable: false),
                    SessionPlanId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionPeriods_SessionPlans_SessionPlanId",
                        column: x => x.SessionPlanId,
                        principalTable: "SessionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionDays_SessionPlanId_Term_Day",
                table: "SessionDays",
                columns: new[] { "SessionPlanId", "Term", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionPeriods_SessionPlanId_Session_Position",
                table: "SessionPeriods",
                columns: new[] { "SessionPlanId", "Session", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionPlans_AcademicYearId",
                table: "SessionPlans",
                column: "AcademicYearId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionPlans_ShiftId",
                table: "SessionPlans",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionDays");

            migrationBuilder.DropTable(
                name: "SessionPeriods");

            migrationBuilder.DropTable(
                name: "SessionPlans");
        }
    }
}
