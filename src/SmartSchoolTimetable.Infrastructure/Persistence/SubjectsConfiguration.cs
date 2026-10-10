using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartSchoolTimetable.Domain.Subjects;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

internal sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects");
        builder.HasKey(subject => subject.Id);
        builder.Property(subject => subject.Name).HasMaxLength(Subject.NameMaxLength).IsRequired();
        builder.Property(subject => subject.NormalizedName).HasMaxLength(Subject.NameMaxLength).IsRequired();
        builder.Property(subject => subject.Notes).HasMaxLength(Subject.NotesMaxLength);
        builder.HasIndex(subject => subject.NormalizedName).IsUnique();
        builder.HasIndex(subject => subject.IsArchived);
        // A required resource cannot be deleted while subjects point at it (reference guard first, then this key).
        builder.HasOne<Domain.Resources.Resource>().WithMany().HasForeignKey(subject => subject.RequiredResourceId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Subjects_ColorIndex", "\"ColorIndex\" BETWEEN 1 AND 10");
            table.HasCheckConstraint("CK_Subjects_Priority", "\"Priority\" BETWEEN 1 AND 5");
        });
        builder.OwnsMany(subject => subject.BlockedPeriods, period =>
        {
            period.ToTable("SubjectBlockedPeriods");
            period.WithOwner().HasForeignKey("SubjectId");
            period.Property<long>("Id").ValueGeneratedOnAdd();
            period.HasKey("Id");
            period.HasIndex("SubjectId", nameof(Domain.SchoolSetup.BlockedPeriod.Day), nameof(Domain.SchoolSetup.BlockedPeriod.LessonNumber)).IsUnique();
        });
        builder.Navigation(subject => subject.BlockedPeriods).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
