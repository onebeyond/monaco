using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Monaco.Template.Domain.Model;

namespace Monaco.Template.Application.Infrastructure.EntityConfigurations;

public class DocumentEntityConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
    }
}