using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartSchoolTimetable.Domain.Settings;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

/// <summary>The owner's stored preferences (one row): theme, default semester, print defaults.</summary>
internal sealed class AppPreferencesConfiguration : IEntityTypeConfiguration<AppPreferences>
{
    public void Configure(EntityTypeBuilder<AppPreferences> builder)
    {
        builder.ToTable("AppPreferences", table =>
            table.HasCheckConstraint("CK_AppPreferences_Semester", "\"DefaultSemester\" IS NULL OR \"DefaultSemester\" IN (1, 2)"));
        builder.HasKey(preferences => preferences.Id);
        builder.Property(preferences => preferences.Id).ValueGeneratedNever();
        builder.Property(preferences => preferences.Theme).HasConversion<string>().HasMaxLength(16);
        builder.Property(preferences => preferences.SectionPaper).HasConversion<string>().HasMaxLength(8);
        builder.Property(preferences => preferences.SectionOrientation).HasConversion<string>().HasMaxLength(16);
        builder.Property(preferences => preferences.TeacherPaper).HasConversion<string>().HasMaxLength(8);
        builder.Property(preferences => preferences.TeacherOrientation).HasConversion<string>().HasMaxLength(16);
        builder.Property(preferences => preferences.SchoolPaper).HasConversion<string>().HasMaxLength(8);
        builder.Property(preferences => preferences.SchoolOrientation).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(preferences => preferences.Section);
        builder.Ignore(preferences => preferences.Teacher);
        builder.Ignore(preferences => preferences.School);
    }
}

/// <summary>Backup folder, automatic backup and how many automatic backups to keep (one row).</summary>
internal sealed class BackupSettingsConfiguration : IEntityTypeConfiguration<BackupSettings>
{
    public void Configure(EntityTypeBuilder<BackupSettings> builder)
    {
        builder.ToTable("BackupSettings", table =>
            table.HasCheckConstraint("CK_BackupSettings_KeepLast", "\"KeepLast\" IN (0, 3, 7, 14, 30)"));
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.Folder).HasMaxLength(BackupSettings.FolderMaxLength);
        builder.Property(settings => settings.LastAppVersion).HasMaxLength(40);
    }
}
