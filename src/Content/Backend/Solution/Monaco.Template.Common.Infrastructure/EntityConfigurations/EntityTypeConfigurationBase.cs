using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Hosting;

namespace Monaco.Template.Common.Infrastructure.EntityConfigurations;

public abstract class EntityTypeConfigurationBase<T> : IEntityTypeConfiguration<T> where T : class
{
    protected readonly IHostEnvironment Environment;

    protected EntityTypeConfigurationBase(IHostEnvironment env)
    {
        Environment = env;
    }

    protected bool CanRunSeed => Environment.IsDevelopment() && !Debugger.IsAttached;

    public abstract void Configure(EntityTypeBuilder<T> builder);
}