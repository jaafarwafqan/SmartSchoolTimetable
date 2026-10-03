using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartSchoolTimetable.Infrastructure.Migrations;

[DbContext(typeof(LocalDbContext))]
[Migration("20261003160000_RemoveEscalatingLoginLockoutFields")]
public sealed class RemoveEscalatingLoginLockoutFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE "Users_WithoutLoginLockout" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
                "OwnerSlot" TEXT NOT NULL,
                "Username" TEXT NOT NULL,
                "NormalizedUsername" TEXT NOT NULL,
                "PasswordSalt" BLOB NOT NULL,
                "PasswordHash" BLOB NOT NULL,
                "PasswordIterations" INTEGER NOT NULL,
                "RecoverySalt" BLOB NOT NULL,
                "RecoveryCodeHash" BLOB NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "RecoveryCodeAcknowledged" INTEGER NOT NULL
            );

            INSERT INTO "Users_WithoutLoginLockout" (
                "Id", "OwnerSlot", "Username", "NormalizedUsername", "PasswordSalt",
                "PasswordHash", "PasswordIterations", "RecoverySalt", "RecoveryCodeHash",
                "CreatedAt", "UpdatedAt", "RecoveryCodeAcknowledged"
            )
            SELECT
                "Id", "OwnerSlot", "Username", "NormalizedUsername", "PasswordSalt",
                "PasswordHash", "PasswordIterations", "RecoverySalt", "RecoveryCodeHash",
                "CreatedAt", "UpdatedAt", "RecoveryCodeAcknowledged"
            FROM "Users";

            DROP TABLE "Users";
            ALTER TABLE "Users_WithoutLoginLockout" RENAME TO "Users";
            CREATE UNIQUE INDEX "IX_Users_NormalizedUsername" ON "Users" ("NormalizedUsername");
            CREATE UNIQUE INDEX "IX_Users_OwnerSlot" ON "Users" ("OwnerSlot");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE "Users_WithLoginLockout" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
                "OwnerSlot" TEXT NOT NULL,
                "Username" TEXT NOT NULL,
                "NormalizedUsername" TEXT NOT NULL,
                "PasswordSalt" BLOB NOT NULL,
                "PasswordHash" BLOB NOT NULL,
                "PasswordIterations" INTEGER NOT NULL,
                "RecoverySalt" BLOB NOT NULL,
                "RecoveryCodeHash" BLOB NOT NULL,
                "FailedLoginCount" INTEGER NOT NULL DEFAULT 0,
                "NextLoginAllowedAt" TEXT NULL,
                "LockoutUntil" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "RecoveryCodeAcknowledged" INTEGER NOT NULL
            );

            INSERT INTO "Users_WithLoginLockout" (
                "Id", "OwnerSlot", "Username", "NormalizedUsername", "PasswordSalt",
                "PasswordHash", "PasswordIterations", "RecoverySalt", "RecoveryCodeHash",
                "CreatedAt", "UpdatedAt", "RecoveryCodeAcknowledged"
            )
            SELECT
                "Id", "OwnerSlot", "Username", "NormalizedUsername", "PasswordSalt",
                "PasswordHash", "PasswordIterations", "RecoverySalt", "RecoveryCodeHash",
                "CreatedAt", "UpdatedAt", "RecoveryCodeAcknowledged"
            FROM "Users";

            DROP TABLE "Users";
            ALTER TABLE "Users_WithLoginLockout" RENAME TO "Users";
            CREATE UNIQUE INDEX "IX_Users_NormalizedUsername" ON "Users" ("NormalizedUsername");
            CREATE UNIQUE INDEX "IX_Users_OwnerSlot" ON "Users" ("OwnerSlot");
            """);
    }
}
