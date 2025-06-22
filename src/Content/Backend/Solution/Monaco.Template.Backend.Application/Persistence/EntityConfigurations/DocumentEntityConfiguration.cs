using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monaco.Template.Backend.Domain.Model.Entities;

namespace Monaco.Template.Backend.Application.Persistence.EntityConfigurations;

internal sealed class DocumentEntityConfiguration : IEntityTypeConfiguration<Document>
{
	public void Configure(EntityTypeBuilder<Document> builder)
	{
	}
}