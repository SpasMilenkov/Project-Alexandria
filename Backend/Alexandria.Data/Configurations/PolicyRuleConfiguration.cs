using System.Text.Json;
using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class PolicyRuleConfiguration : IEntityTypeConfiguration<PolicyRule>
{
    public void Configure(EntityTypeBuilder<PolicyRule> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PolicyId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(e => e.ActionType)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.TriggerType)
            .HasConversion<string>()
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.TriggerValue)
            .HasMaxLength(ValidationConstants.StringLengths.ShortString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.ShortString})")
            .IsRequired();

        builder.Property(e => e.Priority)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(e => e.ApplyOnNewVersion)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.Parameters)
            .HasColumnType("jsonb")
            .HasConversion(
                v => v.RootElement.GetRawText(),
                v => JsonDocument.Parse(v, new JsonDocumentOptions()))
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasColumnType("uuid")
            .IsRequired(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(e => e.DeletedAt)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        // Fast lookup of all rules for a given policy, ordered by priority.
        builder.HasIndex(e => new { e.PolicyId, e.Priority })
            .HasFilter("\"DeletedAt\" IS NULL");

        // Allows finding all rules that match a specific trigger value across policies.
        builder.HasIndex(e => e.TriggerValue)
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.ToTable("PolicyRules");
    }
}