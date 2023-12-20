using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Document Entity")]
public class DocumentTests
{
	[Theory(DisplayName = "New Document succeeds")]
	[AnonymousData]
	public void NewDocumentSucceeds(Guid id,
									string name,
									string extension,
									long size,
									string contentType,
									bool isTemp)
	{
		var sut = new Document(id,
							   name,
							   extension,
							   size,
							   contentType,
							   isTemp);

		sut.Id
		   .Should()
		   .Be(id);
		sut.Name
		   .Should()
		   .Be(name);
		sut.Extension
		   .Should()
		   .Be(extension);
		sut.Size
		   .Should()
		   .Be(size);
		sut.ContentType
		   .Should()
		   .Be(contentType);
		sut.IsTemp
		   .Should()
		   .Be(isTemp);
	}
}