using FluentAssertions;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Monaco.Template.Backend.Application.Features.Product.DTOs;
using Monaco.Template.Backend.Application.Persistence;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Queries - Product", "Get Product Page")]
public class GetProductPageTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();

	[Theory(DisplayName = "Get product page without params succeeds")]
	[AutoDomainData]
	public async Task GetProductPageWithoutParamsSucceeds(List<Domain.Model.Entities.Product> products)
	{
		_dbContextMock.CreateAndSetupDbSetMock(products);
		var query = new GetProductPage.Query([]);

		var sut = new GetProductPage.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .NotBeNull();
		result!.Pager
			   .Count
			   .Should()
			   .Be(products.Count);
		result.Items
			  .Should()
			  .HaveCount(products.Count).And
			  .Contain(x => products.Any(c => c.Title == x.Title)).And
			  .BeInAscendingOrder(x => x.Title);
	}

	[Theory(DisplayName = "Get product page with params succeeds")]
	[AutoDomainData(true)]
	public async Task GetProductPageWithParamsSucceeds(List<Domain.Model.Entities.Product> products)
	{
		_dbContextMock.CreateAndSetupDbSetMock(products);
		var productsSet = products.GetRange(0, 2);
		var query = new GetProductPage.Query([
												 new(nameof(ProductDto.Title),
													 new(productsSet.Select(x => x.Title)
																	.ToArray())),
												 new("expand",
													 new StringValues([
																	   nameof(ProductDto.Company),
																	   nameof(ProductDto.Pictures),
																	   nameof(ProductDto.DefaultPicture)
																	  ])),
												 new("sort", $"-{nameof(ProductDto.Title)}")
											 ]);

		var sut = new GetProductPage.Handler(_dbContextMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .NotBeNull();
		result!.Pager
			   .Count
			   .Should()
			   .Be(productsSet.Count);
		result.Items
			  .Should()
			  .HaveCount(productsSet.Count).And
			  .Contain(x => productsSet.Any(c => c.Title == x.Title)).And
			  .BeInDescendingOrder(x => x.Title);
	}
}