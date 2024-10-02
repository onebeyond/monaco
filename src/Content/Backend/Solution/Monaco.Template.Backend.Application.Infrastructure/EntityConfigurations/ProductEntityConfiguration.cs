using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monaco.Template.Backend.Common.Infrastructure.EntityConfigurations.Extensions;
using Monaco.Template.Backend.Domain.Model;

namespace Monaco.Template.Backend.Application.Infrastructure.EntityConfigurations;

internal sealed class ProductEntityConfiguration : IEntityTypeConfiguration<Product>
{
	public void Configure(EntityTypeBuilder<Product> builder)
	{
		builder.ConfigureIdWithDbGeneratedValue();

		builder.Property(x => x.Title)
			   .IsRequired()
			   .HasMaxLength(Product.TitleLength);

		builder.Property(x => x.Description)
			   .IsRequired()
			   .HasMaxLength(Product.DescriptionLength);

		builder.Property(x => x.Price)
			   .IsRequired()
			   .HasPrecision(10, 2);


		builder.HasOne(x => x.DefaultPicture)
			   .WithOne()
			   .HasForeignKey<Product>(x => x.DefaultPictureId)
			   .IsRequired();

		builder.HasMany(x => x.Pictures)
			   .WithMany()
			   .UsingEntity<Dictionary<string, object>>("ProductPicture",
														x => x.HasOne<Image>()
															  .WithMany()
															  .OnDelete(DeleteBehavior.Cascade),
														x => x.HasOne<Product>()
															  .WithMany()
															  .OnDelete(DeleteBehavior.ClientCascade))
			   .HasIndex($"{nameof(Product.Pictures)}Id")
			   .IsUnique();     //Constraint for single usage of file

		builder.HasIndex(x => x.Title)
			   .IsUnique(false);
	}
}