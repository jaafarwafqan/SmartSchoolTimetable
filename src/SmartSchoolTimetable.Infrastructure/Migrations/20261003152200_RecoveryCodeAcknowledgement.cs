using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartSchoolTimetable.Infrastructure;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations;

[DbContext(typeof(LocalDbContext))]
[Migration("20261003152200_RecoveryCodeAcknowledgement")]
public sealed class RecoveryCodeAcknowledgement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "RecoveryCodeAcknowledged",
            table: "Users",
            type: "INTEGER",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "RecoveryCodeAcknowledged",
            table: "Users");
    }
}
