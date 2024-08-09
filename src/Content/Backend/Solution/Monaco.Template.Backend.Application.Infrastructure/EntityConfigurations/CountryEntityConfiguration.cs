using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Hosting;
using Monaco.Template.Backend.Application.Infrastructure.EntityConfigurations.Seeds;
using Monaco.Template.Backend.Common.Infrastructure.EntityConfigurations;
using Monaco.Template.Backend.Common.Infrastructure.EntityConfigurations.Extensions;
using Monaco.Template.Backend.Domain.Model;

namespace Monaco.Template.Backend.Application.Infrastructure.EntityConfigurations;

public class CountryEntityConfiguration(IHostEnvironment env) : EntityTypeConfigurationBase<Country>(env)
{
	public override void Configure(EntityTypeBuilder<Country> builder)
	{
		builder.ConfigureIdWithDbGeneratedValue();

		builder.Property(x => x.Name)
			   .IsRequired()
			   .HasMaxLength(Country.NameLength);

		if (CanRunSeed)
			builder.HasData(CountrySeed.GetCountries());
	}
}