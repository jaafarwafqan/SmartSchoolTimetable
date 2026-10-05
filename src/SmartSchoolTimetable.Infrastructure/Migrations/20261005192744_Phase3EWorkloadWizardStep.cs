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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SetupProgress"
                SET "CompletedMask" = ("CompletedMask" & ~256) | (("CompletedMask" & 256) >> 1),
                    "SkippedMask" = ("SkippedMask" & ~256) | (("SkippedMask" & 256) >> 1),
                    "CurrentStep" = CASE WHEN "CurrentStep" = 8 THEN 7 ELSE "CurrentStep" END,
                    "Version" = "Version" + 1
                WHERE ("CompletedMask" & 256) <> 0 OR ("SkippedMask" & 256) <> 0 OR "CurrentStep" = 8;
                """);
        }
    }
}
