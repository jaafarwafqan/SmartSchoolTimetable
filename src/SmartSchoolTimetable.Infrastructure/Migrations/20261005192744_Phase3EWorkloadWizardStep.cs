using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3EWorkloadWizardStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SetupProgress"
                SET "CompletedMask" = ("CompletedMask" & ~128) | (("CompletedMask" & 128) << 1),
                    "SkippedMask" = ("SkippedMask" & ~128) | (("SkippedMask" & 128) << 1),
                    "CurrentStep" = CASE WHEN "CurrentStep" = 7 THEN 8 ELSE "CurrentStep" END,
                    "Version" = "Version" + 1
                WHERE ("CompletedMask" & 128) <> 0 OR ("SkippedMask" & 128) <> 0 OR "CurrentStep" = 7;
                """);
        }

        /// <summary>
        /// Back to seven steps: the review (bit 256) returns to bit 128. The workload step's own bit (128) is cleared
        /// first, otherwise a done workload step would show the old review as done.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SetupProgress"
                SET "CompletedMask" = ("CompletedMask" & ~384) | (("CompletedMask" & 256) >> 1),
                    "SkippedMask" = ("SkippedMask" & ~384) | (("SkippedMask" & 256) >> 1),
                    "CurrentStep" = CASE WHEN "CurrentStep" = 8 THEN 7 ELSE "CurrentStep" END,
                    "Version" = "Version" + 1
                WHERE ("CompletedMask" & 256) <> 0 OR ("SkippedMask" & 256) <> 0 OR "CurrentStep" = 8;
                """);
        }
    }
}
