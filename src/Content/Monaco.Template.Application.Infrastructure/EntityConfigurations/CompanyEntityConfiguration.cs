﻿using Monaco.Template.Common.Infrastructure.EntityConfigurations.Extensions;
using Monaco.Template.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Monaco.Template.Application.Infrastructure.EntityConfigurations;

public class CompanyEntityConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ConfigureIdWithDbGeneratedValue();

        builder.Property(x => x.Name)
               .IsRequired()
               .HasMaxLength(100);

		builder.Property(x => x.Email)
			   .IsRequired()
			   .HasMaxLength(255);

        builder.Property(x => x.WebSiteUrl)
               .IsRequired(false)
               .HasMaxLength(300);

        builder.Property(x => x.Version)
               .IsRowVersion();

        builder.OwnsOne(x => x.Address,
						b =>
						{
							b.Property(x => x.Street)
							 .IsRequired(false)
							 .HasMaxLength(100);

							b.Property(x => x.City)
							 .IsRequired(false)
							 .HasMaxLength(100);

							b.Property(x => x.County)
							 .IsRequired(false)
							 .HasMaxLength(100);

							b.Property(x => x.PostCode)
							 .IsRequired(false)
							 .HasMaxLength(10);

							b.HasOne(x => x.Country)
							 .WithMany()
							 .HasForeignKey(x => x.CountryId)
							 .IsRequired();
						});
    }
}