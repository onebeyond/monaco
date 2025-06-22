using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monaco.Template.Backend.Domain.Model.Entities;

namespace Monaco.Template.Backend.Application.Persistence.EntityConfigurations;

internal sealed class ImageEntityConfiguration : IEntityTypeConfiguration<Image>
{
	public void Configure(EntityTypeBuilder<Image> builder)
	{
		builder.Property(x => x.DateTaken)
			   .IsRequired(false);

		builder.OwnsOne(x => x.Dimensions,
						b =>
						{
							b.Property(x => x.Height)
							 .IsRequired();

							b.Property(x => x.Width)
							 .IsRequired();
						});

		builder.OwnsOne(x => x.Position,
						b =>
						{
							b.Property(x => x.Latitude)
							 .IsRequired();

							b.Property(x => x.Longitude)
							 .IsRequired();
						});


		builder.HasOne(x => x.Thumbnail)
			   .WithOne()
			   .HasForeignKey<Image>(x => x.ThumbnailId)
			   .IsRequired(false);
	}
}