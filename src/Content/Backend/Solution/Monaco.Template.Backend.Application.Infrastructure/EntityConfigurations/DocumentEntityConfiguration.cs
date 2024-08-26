using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monaco.Template.Backend.Domain.Model;

namespace Monaco.Template.Backend.Application.Infrastructure.EntityConfigurations;

internal sealed class DocumentEntityConfiguration : IEntityTypeConfiguration<Document>
{
	public void Configure(EntityTypeBuilder<Document> builder)
	{
	}
}