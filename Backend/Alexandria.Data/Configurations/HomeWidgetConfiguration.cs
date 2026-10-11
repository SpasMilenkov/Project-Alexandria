using Alexandria.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alexandria.Data.Configurations;

public class HomeWidgetConfiguration : IEntityTypeConfiguration<HomeWidget>
{
    public void Configure(EntityTypeBuilder<HomeWidget> builder)
    {
        builder.HasKey(widget => widget.Id);

        builder.Property(widget => widget.OptionsJson)
            .HasColumnName("Options")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(widget => widget.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(widget => new { widget.UserId, widget.Layout });

        builder.ToTable("HomeWidgets", table =>
        {
            table.HasCheckConstraint("CK_HomeWidgets_Layout", "\"Layout\" IN (0, 1)");

            table.HasCheckConstraint("CK_HomeWidgets_HorizontalBounds", """
                "Width" >= 1 AND "X" >= 0 AND
                (("Layout" = 0 AND "Width" <= 12 AND "X" <= 12 - "Width") OR
                 ("Layout" = 1 AND "Width" <= 4 AND "X" <= 4 - "Width"))
                """);

            table.HasCheckConstraint("CK_HomeWidgets_VerticalBounds", """
                "Height" BETWEEN 1 AND 12 AND "Y" >= 0 AND "Y" <= 256 - "Height"
                """);

            table.HasCheckConstraint("CK_HomeWidgets_OptionsVersion", "\"OptionsVersion\" >= 1");
            table.HasCheckConstraint("CK_HomeWidgets_OptionsObject", "jsonb_typeof(\"Options\") = 'object'");
        });
    }
}
