using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Hosting;
using DcslGs.Template.Common.Infrastructure.EntityConfigurations;
using DcslGs.Template.Common.Infrastructure.EntityConfigurations.Extensions;
using DcslGs.Template.Domain.Model;
using DcslGs.Template.Infrastructure.EntityConfigurations.Seeds;

namespace DcslGs.Template.Infrastructure.EntityConfigurations;

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