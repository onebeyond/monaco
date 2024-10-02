using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monaco.Template.Backend.Common.Infrastructure.EntityConfigurations.Extensions;
using Monaco.Template.Backend.Domain.Model;

namespace Monaco.Template.Backend.Application.Infrastructure.EntityConfigurations;

internal sealed class CompanyEntityConfiguration : IEntityTypeConfiguration<Company>
{
	public void Configure(EntityTypeBuilder<Company> builder)
	{
		builder.ConfigureIdWithDbGeneratedValue();

		builder.Property(x => x.Name)
			   .IsRequired()
			   .HasMaxLength(Company.NameLength);

		builder.Property(x => x.Email)
			   .IsRequired()
			   .HasMaxLength(Company.EmailLength);

		builder.Property(x => x.WebSiteUrl)
			   .IsRequired(false)
			   .HasMaxLength(Company.WebSiteUrlLength);

		builder.Property(x => x.Version)
			   .IsRowVersion();
		#if (filesSupport)

		builder.HasMany(x => x.Products)
			   .WithOne(x => x.Company)
			   .HasForeignKey(x => x.CompanyId)
			   .OnDelete(DeleteBehavior.Cascade)
			   .IsRequired();
		#endif

		builder.OwnsOne(x => x.Address,
						b =>
						{
							b.Property(x => x.Street)
							 .IsRequired(false)
							 .HasMaxLength(Address.StreetLength);

							b.Property(x => x.City)
							 .IsRequired(false)
							 .HasMaxLength(Address.CityLength);

							b.Property(x => x.County)
							 .IsRequired(false)
							 .HasMaxLength(Address.CountyLength);

							b.Property(x => x.PostCode)
							 .IsRequired(false)
							 .HasMaxLength(Address.PostCodeLength);

							b.HasOne(x => x.Country)
							 .WithMany()
							 .HasForeignKey(x => x.CountryId)
							 .IsRequired();
						});
	}
}