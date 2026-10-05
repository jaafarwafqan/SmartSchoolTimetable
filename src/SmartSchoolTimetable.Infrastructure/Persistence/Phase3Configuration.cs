using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartSchoolTimetable.Domain.Resources;
using SmartSchoolTimetable.Domain.Scheduling;

namespace SmartSchoolTimetable.Infrastructure.Persistence;

internal sealed class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources", table =>
        {
            table.HasCheckConstraint("CK_Resources_Kind", "\"Kind\" BETWEEN 1 AND 4");
            table.HasCheckConstraint("CK_Resources_Capacity", $"\"Capacity\" BETWEEN {Resource.MinCapacity} AND {Resource.MaxCapacity}");
        });
        builder.HasKey(resource => resource.Id);
        builder.Property(resource => resource.Name).HasMaxLength(Resource.NameMaxLength).IsRequired();
        builder.Property(resource => resource.NormalizedName).HasMaxLength(Resource.NameMaxLength).IsRequired();
        builder.Property(resource => resource.Notes).HasMaxLength(Resource.NotesMaxLength);
        builder.HasIndex(resource => resource.NormalizedName).IsUnique();
        builder.HasIndex(resource => resource.IsArchived);
    }
}

/// <summary>The single scheduling profile (Id = 1) and its soft rules, one row per rule key.</summary>
internal sealed class SchedulingProfileConfiguration : IEntityTypeConfiguration<SchedulingProfile>
{
    public const int RuleKeyMaxLength = 40;

    public void Configure(EntityTypeBuilder<SchedulingProfile> builder)
    {
        builder.ToTable("SchedulingProfile");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).ValueGeneratedNever();
        builder.OwnsMany(profile => profile.Rules, rule =>
        {
            rule.ToTable("SchedulingProfileRules", table =>
                table.HasCheckConstraint("CK_SchedulingProfileRules_Weight", $"\"Weight\" BETWEEN {SchedulingProfile.MinWeight} AND {SchedulingProfile.MaxWeight}"));
            rule.WithOwner().HasForeignKey("ProfileId");
            rule.Property<long>("Id").ValueGeneratedOnAdd();
            rule.HasKey("Id");
            rule.Property(item => item.Key).HasMaxLength(RuleKeyMaxLength).IsRequired();
            rule.HasIndex("ProfileId", nameof(SchedulingRule.Key)).IsUnique();
        });
        builder.Navigation(profile => profile.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
