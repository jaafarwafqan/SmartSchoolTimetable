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
