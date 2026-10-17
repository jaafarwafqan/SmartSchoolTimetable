using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <summary>
    /// Corrective data fix (owner's analysis before 1.0.0): «توزيع حصص المادة على أيام الأسبوع» only applied to subjects whose switch was on, and every
    /// subject was created with it off, so the rule never acted. Every existing subject now spreads across days (the switch stays for exceptions), and
    /// the rule weighs «مهم جداً» unless the owner had chosen another weight. No schema change.
    /// </summary>
    public partial class Phase5SpreadAllSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Subjects" SET "SpreadAcrossDays" = 1, "Version" = "Version" + 1 WHERE "SpreadAcrossDays" = 0;
                """);
            // 20 was the old default («مهم»); a weight the owner chose himself (0–19 or above 20) is kept.
            migrationBuilder.Sql("""
                UPDATE "SchedulingProfileRules" SET "Weight" = 40 WHERE "Key" = 'spreadSubjectsAcrossDays' AND "Weight" = 20;
                """);
            migrationBuilder.Sql("""
                UPDATE "SchedulingProfile" SET "ProfileVersion" = "ProfileVersion" + 1, "Version" = "Version" + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Which subjects had the switch off before is not recorded, so the subjects keep spreading; only the weight goes back.
            migrationBuilder.Sql("""
                UPDATE "SchedulingProfileRules" SET "Weight" = 20 WHERE "Key" = 'spreadSubjectsAcrossDays' AND "Weight" = 40;
                """);
        }
    }
}
