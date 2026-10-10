using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5Lifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TimetableVersions_Source",
                table: "TimetableVersions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "TimetableVersions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "TimetableVersions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            // Existing data: the approved version of each year (the only status information before M1) is Approved, the rest are drafts.
            migrationBuilder.Sql("UPDATE \"TimetableVersions\" SET \"Status\" = 2 WHERE \"IsApproved\" = 1;");

            migrationBuilder.AddColumn<long>(
                name: "LockedFromVersionId",
                table: "GenerationRuns",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LockedLessons",
                table: "GenerationRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LocksDropped",
                table: "GenerationRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ParamsJson",
                table: "AuditHistory",
                type: "TEXT",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimetableVersions_ApprovedFlag",
                table: "TimetableVersions",
                sql: "(\"Status\" = 2) = (\"IsApproved\" = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimetableVersions_Source",
                table: "TimetableVersions",
                sql: "\"Source\" BETWEEN 1 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimetableVersions_Status",
                table: "TimetableVersions",
                sql: "\"Status\" BETWEEN 1 AND 3");

            migrationBuilder.CreateIndex(
                name: "IX_AuditHistory_EventType_OccurredAt",
                table: "AuditHistory",
                columns: new[] { "EventType", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TimetableVersions_ApprovedFlag",
                table: "TimetableVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TimetableVersions_Source",
                table: "TimetableVersions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TimetableVersions_Status",
                table: "TimetableVersions");

            migrationBuilder.DropIndex(
                name: "IX_AuditHistory_EventType_OccurredAt",
                table: "AuditHistory");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "TimetableVersions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TimetableVersions");

            migrationBuilder.DropColumn(
                name: "LockedFromVersionId",
                table: "GenerationRuns");

            migrationBuilder.DropColumn(
                name: "LockedLessons",
                table: "GenerationRuns");

            migrationBuilder.DropColumn(
                name: "LocksDropped",
                table: "GenerationRuns");

            migrationBuilder.DropColumn(
                name: "ParamsJson",
                table: "AuditHistory");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TimetableVersions_Source",
                table: "TimetableVersions",
                sql: "\"Source\" BETWEEN 1 AND 2");
        }
    }
}
