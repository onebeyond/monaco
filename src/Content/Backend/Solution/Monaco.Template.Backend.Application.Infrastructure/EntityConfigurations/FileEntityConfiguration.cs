using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monaco.Template.Backend.Common.Infrastructure.EntityConfigurations.Extensions;
using Monaco.Template.Backend.Domain.Model;
using File = Monaco.Template.Backend.Domain.Model.File;

namespace Monaco.Template.Backend.Application.Infrastructure.EntityConfigurations;

public class FileEntityConfiguration : IEntityTypeConfiguration<File>
{
	public void Configure(EntityTypeBuilder<File> builder)
	{
		builder.ConfigureIdWithDefaultAndValueGeneratedNever();

		builder.ToTable("File")
			   .HasDiscriminator<string>("Discriminator")
			   .HasValue<Document>(nameof(Document))
			   .HasValue<Image>(nameof(Image))
			   .IsComplete();

		builder.Property(x => x.Name)
			   .IsRequired()
			   .HasMaxLength(300);

		builder.Property(x => x.Extension)
			   .IsRequired()
			   .HasMaxLength(20);

		builder.Property(x => x.ContentType)
			   .IsRequired()
			   .HasMaxLength(50);

		builder.Property(x => x.Size)
			   .IsRequired();

		builder.Property(x => x.UploadedOn)
			   .IsRequired();

		builder.Property(x => x.IsTemp)
			   .IsRequired();
	}
}