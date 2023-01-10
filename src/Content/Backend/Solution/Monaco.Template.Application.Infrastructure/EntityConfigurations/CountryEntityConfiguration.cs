using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Hosting;
using Monaco.Template.Application.Infrastructure.EntityConfigurations.Seeds;
using Monaco.Template.Common.Infrastructure.EntityConfigurations;
using Monaco.Template.Common.Infrastructure.EntityConfigurations.Extensions;
using Monaco.Template.Domain.Model;

namespace Monaco.Template.Application.Infrastructure.EntityConfigurations;

public class CountryEntityConfiguration : EntityTypeConfigurationBase<Country>
{
    public CountryEntityConfiguration(IHostEnvironment env) : base(env)
    {
    }

    public override void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ConfigureIdWithDbGeneratedValue();

        builder.Property(x => x.Name)
               .IsRequired()
               .HasMaxLength(100);
			
        if (CanRunSeed)
            builder.HasData(CountrySeed.GetCountries());
    }
}