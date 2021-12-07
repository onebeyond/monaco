using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DcslGs.Template.Common.Domain.Model;

namespace DcslGs.Template.Common.Infrastructure.EntityConfigurations.Extensions;

public static class EntityTypeBuilderExtensions
{
    public static void ConfigureId<T>(this EntityTypeBuilder<T> builder) where T : Entity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
               .IsRequired();
    }

    public static void ConfigureIdWithDefaultAndValueGeneratedNever<T>(this EntityTypeBuilder<T> builder) where T : Entity
    {
        builder.ConfigureId();
        builder.Property(x => x.Id)
               .ValueGeneratedNever();
    }

    public static void ConfigureIdWithDbGeneratedValue<T>(this EntityTypeBuilder<T> builder) where T : Entity
    {
        builder.ConfigureId();
        builder.Property(x => x.Id)
               .ValueGeneratedOnAdd();
    }

    public static void ConfigureIdWithSequence<T>(this EntityTypeBuilder<T> builder) where T : Entity
    {
        builder.ConfigureId();
        builder.Property(x => x.Id)
               .UseHiLo($"{typeof(T).Name}Sequence");
    }

    public static void ConfigureIdWithIdentity<T>(this EntityTypeBuilder<T> builder) where T : Entity
    {
        builder.ConfigureId();
        builder.Property(x => x.Id)
               .UseIdentityColumn();
    }

    public static DataBuilder<TEntity> HasData<TEntity>(this EntityTypeBuilder<TEntity> source, Func<TEntity>[] dataFuncs) where TEntity : class
    {
        return source.HasData(dataFuncs.Select(func => func()));
    }
}