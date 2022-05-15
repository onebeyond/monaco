using Monaco.Template.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Monaco.Template.Application.Infrastructure.EntityConfigurations;

public class ImageEntityConfiguration : IEntityTypeConfiguration<Image>
{
    public void Configure(EntityTypeBuilder<Image> builder)
    {
        builder.Property(x => x.DateTaken)
               .IsRequired(false);

        builder.Property(x => x.Height)
               .IsRequired();

        builder.Property(x => x.Width)
               .IsRequired();

        builder.HasOne(x => x.Thumbnail)
               .WithOne()
               .HasForeignKey<Image>(x => x.ThumbnailId)
               .IsRequired(false);
    }
}