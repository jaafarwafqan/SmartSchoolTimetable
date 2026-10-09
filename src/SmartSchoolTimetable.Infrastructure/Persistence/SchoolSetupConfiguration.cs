using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

internal sealed class SchoolProfileConfiguration : IEntityTypeConfiguration<SchoolProfile>
{
    public void Configure(EntityTypeBuilder<SchoolProfile> builder)
    {
        builder.ToTable("SchoolProfile");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).ValueGeneratedNever();
        builder.Property(profile => profile.Name).HasMaxLength(SchoolProfile.NameMaxLength).IsRequired();
        builder.Property(profile => profile.PrincipalName).HasMaxLength(SchoolProfile.PersonNameMaxLength);
        builder.Property(profile => profile.ScheduleOfficerName).HasMaxLength(SchoolProfile.PersonNameMaxLength);
        builder.Property(profile => profile.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(profile => profile.SchoolType).HasConversion<string>().HasMaxLength(32);
        builder.Property(profile => profile.StudyType).HasConversion<string>().HasMaxLength(32);
        builder.Property(profile => profile.NumeralSystem).HasConversion<string>().HasMaxLength(32);
        builder.Property(profile => profile.CalendarDisplay).HasConversion<string>().HasMaxLength(32);
        builder.Ignore(profile => profile.IsFilled);
        ConfigureAsset(builder.OwnsOne(profile => profile.Logo), "Logo");
        ConfigureAsset(builder.OwnsOne(profile => profile.Stamp), "Stamp");
    }

    private static void ConfigureAsset(OwnedNavigationBuilder<SchoolProfile, SchoolAsset> asset, string prefix)
    {
        asset.Property(value => value.StoredFileName).HasColumnName($"{prefix}StoredFileName").HasMaxLength(100);
        asset.Property(value => value.ContentType).HasColumnName($"{prefix}ContentType").HasMaxLength(50);
        asset.Property(value => value.SizeBytes).HasColumnName($"{prefix}SizeBytes");
        asset.Property(value => value.UploadedAt).HasColumnName($"{prefix}UploadedAt");
    }
}

internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts");
        builder.HasKey(shift => shift.Id);
        builder.Property(shift => shift.Name).HasMaxLength(Shift.NameMaxLength).IsRequired();
        builder.Property(shift => shift.NormalizedName).HasMaxLength(Shift.NameMaxLength).IsRequired();
        builder.HasIndex(shift => new { shift.AcademicYearId, shift.NormalizedName }).IsUnique();
        builder.HasIndex(shift => new { shift.AcademicYearId, shift.DisplayOrder });
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(shift => shift.AcademicYearId).OnDelete(DeleteBehavior.Cascade);
        builder.OwnsMany(shift => shift.Periods, period =>
        {
            period.ToTable("LessonPeriods");
            period.WithOwner().HasForeignKey("ShiftId");
            period.HasKey(value => value.Id);
            period.Property(value => value.Id).ValueGeneratedOnAdd();
            period.Property(value => value.Kind).HasConversion<string>().HasMaxLength(16);
            period.HasIndex("ShiftId", nameof(LessonPeriod.Position)).IsUnique();
        });
        builder.Navigation(shift => shift.Periods).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Property(shift => shift.Kind).HasConversion<string>().HasMaxLength(16);
        // Per-day lesson counts (ADR 0020): only days that differ from the shift's lesson count are stored.
        builder.OwnsMany(shift => shift.DayLessonOverrides, day =>
        {
            day.ToTable("ShiftDayLessons");
            day.WithOwner().HasForeignKey("ShiftId");
            day.Property<long>("Id").ValueGeneratedOnAdd();
            day.HasKey("Id");
            day.HasIndex("ShiftId", nameof(DayLessons.Day)).IsUnique();
        });
        builder.Navigation(shift => shift.DayLessonOverrides).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

/// <summary>R3 daily sessions: one optional plan per year; no row means a single session (the behaviour before R3).</summary>
internal sealed class SessionPlanConfiguration : IEntityTypeConfiguration<SessionPlan>
{
    public void Configure(EntityTypeBuilder<SessionPlan> builder)
    {
        builder.ToTable("SessionPlans");
        builder.HasKey(plan => plan.Id);
        builder.HasIndex(plan => plan.AcademicYearId).IsUnique();
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(plan => plan.AcademicYearId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Shift>().WithMany().HasForeignKey(plan => plan.ShiftId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(plan => plan.System).HasConversion<int>();
        builder.OwnsMany(plan => plan.Periods, period =>
        {
            period.ToTable("SessionPeriods");
            period.WithOwner().HasForeignKey("SessionPlanId");
            period.Property<long>("Id").ValueGeneratedOnAdd();
            period.HasKey("Id");
            period.Property(value => value.Session).HasConversion<int>();
            period.Property(value => value.Kind).HasConversion<string>().HasMaxLength(16);
            period.HasIndex("SessionPlanId", nameof(SessionPeriod.Session), nameof(SessionPeriod.Position)).IsUnique();
        });
        builder.Navigation(plan => plan.Periods).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.OwnsMany(plan => plan.Days, day =>
        {
            day.ToTable("SessionDays");
            day.WithOwner().HasForeignKey("SessionPlanId");
            day.Property<long>("Id").ValueGeneratedOnAdd();
            day.HasKey("Id");
            day.Property(value => value.Session).HasConversion<int>();
            day.HasIndex("SessionPlanId", nameof(SessionDay.Term), nameof(SessionDay.Day)).IsUnique();
        });
        builder.Navigation(plan => plan.Days).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class WorkingWeekConfiguration : IEntityTypeConfiguration<WorkingWeek>
{
    public void Configure(EntityTypeBuilder<WorkingWeek> builder)
    {
        builder.ToTable("WorkingWeek");
        builder.HasKey(week => week.Id);
        builder.Property(week => week.Id).ValueGeneratedNever();
    }
}

internal sealed class BellSettingsConfiguration : IEntityTypeConfiguration<BellSettings>
{
    public void Configure(EntityTypeBuilder<BellSettings> builder)
    {
        builder.ToTable("BellSettings");
        builder.HasKey(settings => settings.Id);
        builder.Property(settings => settings.Id).ValueGeneratedNever();
        builder.Property(settings => settings.Tone).HasConversion<string>().HasMaxLength(16);
    }
}

internal sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("AcademicYears");
        builder.HasKey(year => year.Id);
        builder.Property(year => year.Label).HasMaxLength(AcademicYear.LabelMaxLength).IsRequired();
        builder.Property(year => year.NormalizedLabel).HasMaxLength(AcademicYear.LabelMaxLength).IsRequired();
        builder.HasIndex(year => year.NormalizedLabel).IsUnique();
        // At most one current year, enforced by the database as well as the service.
        builder.HasIndex(year => year.IsCurrent).IsUnique().HasFilter("\"IsCurrent\" = 1");
        builder.Ignore(year => year.CurrentTerm);
        builder.OwnsMany(year => year.Terms, term =>
        {
            term.ToTable("Terms");
            term.WithOwner().HasForeignKey("AcademicYearId");
            term.HasKey(value => value.Id);
            term.Property(value => value.Id).ValueGeneratedOnAdd();
            term.Property(value => value.Name).HasMaxLength(AcademicYear.TermNameMaxLength).IsRequired();
            term.Property(value => value.NormalizedName).HasMaxLength(AcademicYear.TermNameMaxLength).IsRequired();
            term.HasIndex("AcademicYearId", nameof(Term.NormalizedName)).IsUnique();
        });
        builder.Navigation(year => year.Terms).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("Stages");
        builder.HasKey(stage => stage.Id);
        builder.Property(stage => stage.Name).HasMaxLength(Stage.NameMaxLength).IsRequired();
        builder.Property(stage => stage.NormalizedName).HasMaxLength(Stage.NameMaxLength).IsRequired();
        builder.Property(stage => stage.TemplateKey).HasMaxLength(Stage.TemplateKeyMaxLength);
        builder.HasIndex(stage => new { stage.AcademicYearId, stage.NormalizedName }).IsUnique();
        builder.HasIndex(stage => new { stage.AcademicYearId, stage.DisplayOrder });
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(stage => stage.AcademicYearId).OnDelete(DeleteBehavior.Cascade);
        // ADR 0027: the stage's own lessons per working day; a day without a row inherits the shift's count.
        builder.OwnsMany(stage => stage.DayLessonCounts, day =>
        {
            day.ToTable("StageDayLessons", table => table.HasCheckConstraint("CK_StageDayLessons_Lessons", "\"Lessons\" >= 1"));
            day.WithOwner().HasForeignKey("StageId");
            day.Property<long>("Id").ValueGeneratedOnAdd();
            day.HasKey("Id");
            day.HasIndex("StageId", nameof(DayLessons.Day)).IsUnique();
        });
        builder.Navigation(stage => stage.DayLessonCounts).HasField("_dayLessons").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections");
        builder.HasKey(section => section.Id);
        builder.Property(section => section.Label).HasMaxLength(Section.LabelMaxLength).IsRequired();
        builder.Property(section => section.NormalizedLabel).HasMaxLength(Section.LabelMaxLength).IsRequired();
        builder.HasIndex(section => new { section.StageId, section.NormalizedLabel }).IsUnique();
        builder.HasOne<Stage>().WithMany().HasForeignKey(section => section.StageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Shift>().WithMany().HasForeignKey(section => section.ShiftId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SetupProgressConfiguration : IEntityTypeConfiguration<SetupProgress>
{
    public void Configure(EntityTypeBuilder<SetupProgress> builder)
    {
        builder.ToTable("SetupProgress");
        builder.HasKey(progress => progress.Id);
        builder.Property(progress => progress.Id).ValueGeneratedNever();
        builder.Ignore(progress => progress.CompletedSteps);
        builder.Ignore(progress => progress.SkippedSteps);
    }
}
