using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartSchoolTimetable.Domain.SchoolSetup;
using SmartSchoolTimetable.Domain.Teachers;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

internal sealed class TeacherConfiguration : IEntityTypeConfiguration<Teacher>
{
    public void Configure(EntityTypeBuilder<Teacher> builder)
    {
        builder.ToTable("Teachers");
        builder.HasKey(teacher => teacher.Id);
        builder.Property(teacher => teacher.FullName).HasMaxLength(Teacher.FullNameMaxLength).IsRequired();
        builder.Property(teacher => teacher.NormalizedFullName).HasMaxLength(Teacher.FullNameMaxLength).IsRequired();
        builder.Property(teacher => teacher.ShortName).HasMaxLength(Teacher.ShortNameMaxLength).IsRequired();
        builder.Property(teacher => teacher.NormalizedShortName).HasMaxLength(Teacher.ShortNameMaxLength).IsRequired();
        builder.Property(teacher => teacher.ReleaseReason).HasMaxLength(Teacher.ReasonMaxLength);
        builder.Property(teacher => teacher.Notes).HasMaxLength(Teacher.NotesMaxLength);
        builder.Ignore(teacher => teacher.OffDays);
        builder.HasIndex(teacher => teacher.NormalizedShortName).IsUnique();
        builder.HasIndex(teacher => teacher.NormalizedFullName);
        builder.HasIndex(teacher => teacher.IsArchived);
        builder.OwnsMany(teacher => teacher.BlockedPeriods, period =>
        {
            period.ToTable("TeacherBlockedPeriods");
            period.WithOwner().HasForeignKey("TeacherId");
            period.Property<long>("Id").ValueGeneratedOnAdd();
            period.HasKey("Id");
            period.HasIndex("TeacherId", nameof(BlockedPeriod.Day), nameof(BlockedPeriod.LessonNumber)).IsUnique();
        });
        builder.Navigation(teacher => teacher.BlockedPeriods).UsePropertyAccessMode(PropertyAccessMode.Field);
        // Specializations go with a deleted subject (they only describe the teacher; DECISIONS_PENDING #53).
        builder.OwnsMany(teacher => teacher.Specializations, specialization =>
        {
            specialization.ToTable("TeacherSpecializations");
            specialization.WithOwner().HasForeignKey("TeacherId");
            specialization.HasKey("TeacherId", nameof(TeacherSpecialization.SubjectId));
            specialization.HasIndex(item => item.SubjectId);
            specialization.HasOne<Domain.Subjects.Subject>().WithMany().HasForeignKey(item => item.SubjectId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Navigation(teacher => teacher.Specializations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
