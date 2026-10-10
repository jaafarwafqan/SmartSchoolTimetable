using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3CWorkload : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkloadAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SectionId = table.Column<long>(type: "INTEGER", nullable: false),
                    CurriculumEntryId = table.Column<long>(type: "INTEGER", nullable: false),
                    TeacherId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkloadAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkloadAssignments_CurriculumEntries_CurriculumEntryId",
                        column: x => x.CurriculumEntryId,
                        principalTable: "CurriculumEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkloadAssignments_Sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "Sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkloadAssignments_Teachers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "Teachers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkloadAssignments_Active_Section_Entry",
                table: "WorkloadAssignments",
                columns: new[] { "SectionId", "CurriculumEntryId" },
                unique: true,
                filter: "\"IsArchived\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WorkloadAssignments_CurriculumEntryId",
                table: "WorkloadAssignments",
                column: "CurriculumEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkloadAssignments_TeacherId_IsArchived",
                table: "WorkloadAssignments",
                columns: new[] { "TeacherId", "IsArchived" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkloadAssignments");
        }
    }
}
