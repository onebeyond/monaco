using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Domain.Model.Contracts;

namespace Monaco.Template.Backend.Common.Infrastructure.EntityConfigurations.Extensions;

public static class EntityTypeBuilderExtensions
{
	extension<T>(EntityTypeBuilder<T> builder) where T : Entity
	{
		public void ConfigureId()
		{
			builder.HasKey(x => x.Id);
			builder.Property(x => x.Id)
				   .IsRequired();
		}

		public void ConfigureIdWithValueGeneratedNever()
		{
			builder.ConfigureId();
			builder.Property(x => x.Id)
				   .ValueGeneratedNever();
		}

		public void ConfigureIdWithDbGeneratedValue()
		{
			builder.ConfigureId();
			builder.Property(x => x.Id)
				   .ValueGeneratedOnAdd();
		}

		public void ConfigureIdWithSequence()
		{
			builder.ConfigureId();
			builder.Property(x => x.Id)
				   .UseHiLo($"{typeof(T).Name}Sequence");
		}

		public void ConfigureIdWithIdentity()
		{
			builder.ConfigureId();
			builder.Property(x => x.Id)
				   .UseIdentityColumn();
		}
	}

	extension<T>(EntityTypeBuilder<T> builder) where T : Entity, IAuditable
	{
		public void ConfigureAuditableFields()
		{
			builder.Property(x => x.CreatedAtUtc)
				   .IsRequired()
				   .HasPrecision(7);

			builder.Property(x => x.CreatedBy)
				   .IsRequired(false)
				   .HasMaxLength(IAuditable.ActorMaxLength);

			builder.Property(x => x.ModifiedAtUtc)
				   .IsRequired(false)
				   .HasPrecision(7);

			builder.Property(x => x.ModifiedBy)
				   .IsRequired(false)
				   .HasMaxLength(IAuditable.ActorMaxLength);
		}
	}

	extension<TEntity>(EntityTypeBuilder<TEntity> source) where TEntity : class
	{
		public DataBuilder<TEntity> HasData(Func<TEntity>[] dataFuncs) =>
			source.HasData(dataFuncs.Select(func => func()));
	}
}