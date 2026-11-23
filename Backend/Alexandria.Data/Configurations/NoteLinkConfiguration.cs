using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class NoteLinkConfiguration : IEntityTypeConfiguration<NoteLink>
{
    public void Configure(EntityTypeBuilder<NoteLink> builder)
    {
        builder.HasKey(e => new { e.ContentObjectId, e.TargetKind, e.TargetId });

        builder.Property(e => e.TargetKind)
            .IsRequired();

        builder.Property(e => e.Snippet)
            .HasMaxLength(ValidationConstants.StringLengths.LongString)
            .HasColumnType($"varchar({ValidationConstants.StringLengths.LongString})")
            .IsRequired();

        // "Mentioned in" starts from the target.
        builder.HasIndex(e => new { e.TargetKind, e.TargetId });

        builder.ToTable("NoteLinks");
    }
}
