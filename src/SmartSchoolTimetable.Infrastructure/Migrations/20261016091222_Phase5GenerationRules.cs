using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5GenerationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Subjects_DailyCap",
                table: "Subjects");

            migrationBuilder.AddColumn<int>(
                name: "MaxConsecutive",
                table: "Teachers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "BalanceHard",
                table: "SchedulingProfile",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DefaultMaxConsecutive",
                table: "SchedulingProfile",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "SubjectCapOverrides",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeId = table.Column<long>(type: "INTEGER", nullable: false),
                    Cap = table.Column<int>(type: "INTEGER", nullable: false),
                    SubjectId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubjectCapOverrides", x => x.Id);
                    table.CheckConstraint("CK_SubjectCapOverrides_Cap", "\"Cap\" BETWEEN 1 AND 6");
                    table.ForeignKey(
                        name: "FK_SubjectCapOverrides_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Teachers_MaxConsecutive",
                table: "Teachers",
                sql: "\"MaxConsecutive\" IS NULL OR \"MaxConsecutive\" IN (2, 3)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subjects_DailyCap",
                table: "Subjects",
                sql: "\"DailyCap\" IS NULL OR \"DailyCap\" BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SchedulingProfile_DefaultMaxConsecutive",
                table: "SchedulingProfile",
                sql: "\"DefaultMaxConsecutive\" IN (2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_SubjectCapOverrides_SubjectId_Scope_ScopeId",
                table: "SubjectCapOverrides",
                columns: new[] { "SubjectId", "Scope", "ScopeId" },
                unique: true);

            // Data: the priorities of an existing school get the three new rules. «تجنّب فراغات المعلم» (متتالية) is switched off when it still has the
            // old default (on, weight 30) and «توزيع حصص المعلم بفواصل» takes its place; a school that had set its own weight keeps «متتالية» and gets the
            // new spread rule off (the two are mutually exclusive). The profile version grows so a saved timetable shows that the priorities changed.
            migrationBuilder.Sql("""
                UPDATE "SchedulingProfileRules" SET "Enabled" = 0 WHERE "Key" = 'avoidTeacherGaps' AND "Enabled" = 1 AND "Weight" = 30;
                """);
            migrationBuilder.Sql("""
                INSERT INTO "SchedulingProfileRules" ("ProfileId", "Key", "Enabled", "Weight")
                SELECT p."Id", 'spreadTeacherLessons',
                       CASE WHEN EXISTS (SELECT 1 FROM "SchedulingProfileRules" r WHERE r."ProfileId" = p."Id" AND r."Key" = 'avoidTeacherGaps' AND r."Enabled" = 1 AND r."Weight" > 0) THEN 0 ELSE 1 END,
                       30
                FROM "SchedulingProfile" p
                WHERE NOT EXISTS (SELECT 1 FROM "SchedulingProfileRules" r WHERE r."ProfileId" = p."Id" AND r."Key" = 'spreadTeacherLessons');
                """);
            migrationBuilder.Sql("""
                INSERT INTO "SchedulingProfileRules" ("ProfileId", "Key", "Enabled", "Weight")
                SELECT p."Id", 'balanceTeacherDays', 1, 40 FROM "SchedulingProfile" p
                WHERE NOT EXISTS (SELECT 1 FROM "SchedulingProfileRules" r WHERE r."ProfileId" = p."Id" AND r."Key" = 'balanceTeacherDays');
                """);
            migrationBuilder.Sql("""
                INSERT INTO "SchedulingProfileRules" ("ProfileId", "Key", "Enabled", "Weight")
                SELECT p."Id", 'fairLastPeriods', 1, 20 FROM "SchedulingProfile" p
                WHERE NOT EXISTS (SELECT 1 FROM "SchedulingProfileRules" r WHERE r."ProfileId" = p."Id" AND r."Key" = 'fairLastPeriods');
                """);
            migrationBuilder.Sql("""
                UPDATE "SchedulingProfile" SET "ProfileVersion" = "ProfileVersion" + 1, "Version" = "Version" + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "SchedulingProfileRules" WHERE "Key" IN ('spreadTeacherLessons', 'balanceTeacherDays', 'fairLastPeriods');
                """);
            migrationBuilder.DropTable(
                name: "SubjectCapOverrides");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Teachers_MaxConsecutive",
                table: "Teachers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Subjects_DailyCap",
                table: "Subjects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SchedulingProfile_DefaultMaxConsecutive",
                table: "SchedulingProfile");

            migrationBuilder.DropColumn(
                name: "MaxConsecutive",
                table: "Teachers");

            migrationBuilder.DropColumn(
                name: "BalanceHard",
                table: "SchedulingProfile");

            migrationBuilder.DropColumn(
                name: "DefaultMaxConsecutive",
                table: "SchedulingProfile");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Subjects_DailyCap",
                table: "Subjects",
                sql: "\"DailyCap\" IS NULL OR \"DailyCap\" BETWEEN 2 AND 6");
        }
    }
}
