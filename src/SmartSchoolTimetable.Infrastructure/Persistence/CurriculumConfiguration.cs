using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartSchoolTimetable.Domain.Curriculum;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Subjects;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

/// <summary>Curriculum entries: deliberately NO unique index on (stage, subject); a subject may repeat (ADR 0021).</summary>
internal sealed class CurriculumEntryConfiguration : IEntityTypeConfiguration<CurriculumEntry>
{
    public void Configure(EntityTypeBuilder<CurriculumEntry> builder)
    {
        builder.ToTable("CurriculumEntries", table =>
            table.HasCheckConstraint("CK_CurriculumEntries_WeeklyLessons", "\"WeeklyLessons\" BETWEEN 1 AND 15"));
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Label).HasMaxLength(CurriculumEntry.LabelMaxLength);
        builder.Property(entry => entry.NormalizedLabel).HasMaxLength(CurriculumEntry.LabelMaxLength).IsRequired();
        builder.Property(entry => entry.Notes).HasMaxLength(CurriculumEntry.NotesMaxLength);
        builder.HasIndex(entry => new { entry.StageId, entry.SubjectId });
        builder.HasIndex(entry => entry.SubjectId);
        builder.HasOne<Stage>().WithMany().HasForeignKey(entry => entry.StageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subject>().WithMany().HasForeignKey(entry => entry.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
