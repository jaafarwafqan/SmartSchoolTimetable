using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2CDisplayOrderIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stages_AcademicYearId_DisplayOrder",
                table: "Stages");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_AcademicYearId_DisplayOrder",
                table: "Shifts");

            migrationBuilder.CreateIndex(
                name: "IX_Stages_AcademicYearId_DisplayOrder",
                table: "Stages",
                columns: new[] { "AcademicYearId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_AcademicYearId_DisplayOrder",
                table: "Shifts",
                columns: new[] { "AcademicYearId", "DisplayOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stages_AcademicYearId_DisplayOrder",
                table: "Stages");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_AcademicYearId_DisplayOrder",
                table: "Shifts");

            migrationBuilder.CreateIndex(
                name: "IX_Stages_AcademicYearId_DisplayOrder",
                table: "Stages",
                columns: new[] { "AcademicYearId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_AcademicYearId_DisplayOrder",
                table: "Shifts",
                columns: new[] { "AcademicYearId", "DisplayOrder" },
                unique: true);
        }
    }
}
