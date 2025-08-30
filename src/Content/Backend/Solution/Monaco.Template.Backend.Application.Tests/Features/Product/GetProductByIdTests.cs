using AwesomeAssertions;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Queries - Product", "Get Product By Id")]
public class GetProductByIdTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();

	[Theory(DisplayName = "Get existing product by Id succeeds")]
	[AutoDomainData(true)]
	public async Task GetExistingProductByIdSucceeds(List<Domain.Model.Entities.Product> products)
	{
		_dbContextMock.CreateAndSetupDbSetMock(products);
		var product = products.First();
		var query = new GetProductById.Query(product.Id);

		var sut = new GetProductById.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, CancellationToken.None);

		result.Should()
			  .NotBeNull();
		result!.Title
			   .Should()
			   .Be(product.Title);
	}

	[Theory(DisplayName = "Get non-existing product by Id fails")]
	[AutoDomainData(true)]
	public async Task GetNonExistingProductByIdFails(List<Domain.Model.Entities.Product> products)
	{
		_dbContextMock.CreateAndSetupDbSetMock(products);
		var query = new GetProductById.Query(Guid.NewGuid());

		var sut = new GetProductById.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, CancellationToken.None);

		result.Should()
			  .BeNull();
	}
}