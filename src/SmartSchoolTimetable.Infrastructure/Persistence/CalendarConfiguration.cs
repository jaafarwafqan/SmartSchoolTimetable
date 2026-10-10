using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartSchoolTimetable.Domain.Calendar;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

internal sealed class CalendarDayConfiguration : IEntityTypeConfiguration<CalendarDay>
{
    public void Configure(EntityTypeBuilder<CalendarDay> builder)
    {
        builder.ToTable("CalendarDays");
        builder.HasKey(day => day.Id);
        builder.Property(day => day.Title).HasMaxLength(CalendarDay.TitleMaxLength).IsRequired();
        builder.Property(day => day.NormalizedTitle).HasMaxLength(CalendarDay.TitleMaxLength).IsRequired();
        builder.Property(day => day.Kind).HasConversion<string>().HasMaxLength(32);
        builder.Property(day => day.Source).HasConversion<string>().HasMaxLength(32).HasDefaultValue(CalendarDaySource.Manual);
        builder.Property(day => day.TemplateKey).HasMaxLength(64);
        builder.Property(day => day.IsApproximate).HasDefaultValue(false);
        builder.Property(day => day.IsEnabled).HasDefaultValue(true);
        builder.HasIndex(day => new { day.StartDate, day.EndDate });
        builder.ToTable(table => table.HasCheckConstraint("CK_CalendarDays_Range", "\"EndDate\" >= \"StartDate\""));
    }
}
