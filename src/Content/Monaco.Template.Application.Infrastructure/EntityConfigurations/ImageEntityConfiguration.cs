using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monaco.Template.Domain.Model;

namespace Monaco.Template.Application.Infrastructure.EntityConfigurations;

public class ImageEntityConfiguration : IEntityTypeConfiguration<Image>
{
    public void Configure(EntityTypeBuilder<Image> builder)
    {
		builder.Property(x => x.Height)
			   .IsRequired();

        builder.Property(x => x.Width)
               .IsRequired();

		builder.Property(x => x.DateTaken)
			   .IsRequired(false);

		builder.Property(x => x.GpsLatitude)
			   .IsRequired(false);

		builder.Property(x => x.GpsLongitude)
			   .IsRequired(false);

        builder.HasOne(x => x.Thumbnail)
               .WithOne()
               .HasForeignKey<Image>(x => x.ThumbnailId)
               .IsRequired(false);
    }
}