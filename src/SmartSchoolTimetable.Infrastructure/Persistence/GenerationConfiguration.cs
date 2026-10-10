using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartSchoolTimetable.Domain.Generation;
using SmartSchoolTimetable.Domain.SchoolSetup;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

/// <summary>Generation runs (Phase 4 §6): one row per generation, indexed by year and by status for the single-active check.</summary>
internal sealed class GenerationRunConfiguration : IEntityTypeConfiguration<GenerationRun>
{
    public void Configure(EntityTypeBuilder<GenerationRun> builder)
    {
        builder.ToTable("GenerationRuns", table => table.HasCheckConstraint("CK_GenerationRuns_Status", "\"Status\" BETWEEN 1 AND 9"));
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Mode).HasMaxLength(GenerationRun.ModeMaxLength).IsRequired();
        builder.Property(run => run.SolverVersion).HasMaxLength(GenerationRun.SolverVersionMaxLength).IsRequired();
        builder.Property(run => run.SolverParameters).HasMaxLength(GenerationRun.ParametersMaxLength).IsRequired();
        builder.Property(run => run.InputHash).HasMaxLength(GenerationRun.HashMaxLength).IsRequired();
        builder.Property(run => run.ErrorCode).HasMaxLength(GenerationRun.CodeMaxLength);
        builder.HasIndex(run => new { run.AcademicYearId, run.Id });
        builder.HasIndex(run => run.Status);
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(run => run.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Saved timetables and their lessons (owned rows in TimetableLessons, indexed for lookups by section and by teacher).
/// Lessons keep plain ids, no foreign keys: a version is a self-contained record with its own input snapshot.
/// </summary>
internal sealed class TimetableVersionConfiguration : IEntityTypeConfiguration<TimetableVersion>
{
    public void Configure(EntityTypeBuilder<TimetableVersion> builder)
    {
        builder.ToTable("TimetableVersions", table => table.HasCheckConstraint("CK_TimetableVersions_Source", "\"Source\" BETWEEN 1 AND 2"));
        builder.HasKey(version => version.Id);
        builder.Property(version => version.Mode).HasMaxLength(GenerationRun.ModeMaxLength).IsRequired();
        builder.Property(version => version.InputHash).HasMaxLength(GenerationRun.HashMaxLength).IsRequired();
        builder.Property(version => version.InputJson).IsRequired();
        builder.Property(version => version.Note).HasMaxLength(TimetableVersion.NoteMaxLength);
        builder.HasIndex(version => new { version.AcademicYearId, version.Number }).IsUnique();
        // At most one approved version per year.
        builder.HasIndex(version => version.AcademicYearId).IsUnique().HasFilter("\"IsApproved\" = 1").HasDatabaseName("IX_TimetableVersions_Approved_Year");
        builder.HasOne<AcademicYear>().WithMany().HasForeignKey(version => version.AcademicYearId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GenerationRun>().WithMany().HasForeignKey(version => version.GenerationRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TimetableVersion>().WithMany().HasForeignKey(version => version.ParentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(version => version.Lessons, lesson =>
        {
            lesson.ToTable("TimetableLessons", table =>
                table.HasCheckConstraint("CK_TimetableLessons_Slot", $"\"Day\" BETWEEN 1 AND 7 AND \"LessonNumber\" BETWEEN 1 AND {Shift.MaxLessons}"));
            lesson.WithOwner().HasForeignKey("TimetableVersionId");
            lesson.Property<long>("Id").ValueGeneratedOnAdd();
            lesson.HasKey("Id");
            lesson.HasIndex("TimetableVersionId", nameof(TimetableLesson.SectionId));
            lesson.HasIndex("TimetableVersionId", nameof(TimetableLesson.TeacherId));
        });
        builder.Navigation(version => version.Lessons).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
