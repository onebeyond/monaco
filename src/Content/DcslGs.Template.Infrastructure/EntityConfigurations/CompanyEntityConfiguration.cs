using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DcslGs.Template.Common.Infrastructure.EntityConfigurations.Extensions;
using DcslGs.Template.Domain.Model;

namespace DcslGs.Template.Infrastructure.EntityConfigurations;

public class CompanyEntityConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ConfigureIdWithDbGeneratedValue();

        builder.Property(x => x.Name)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(x => x.WebSiteUrl)
               .IsRequired(false)
               .HasMaxLength(300);

        builder.Property(x => x.Address)
               .IsRequired(false)
               .HasMaxLength(100);

        builder.Property(x => x.City)
               .IsRequired(false)
               .HasMaxLength(100);

        builder.Property(x => x.County)
               .IsRequired(false)
               .HasMaxLength(100);

        builder.Property(x => x.PostCode)
               .IsRequired(false)
               .HasMaxLength(10);


        builder.HasOne(x => x.Country)
               .WithMany()
               .HasForeignKey(x => x.CountryId)
               .IsRequired();
    }
}