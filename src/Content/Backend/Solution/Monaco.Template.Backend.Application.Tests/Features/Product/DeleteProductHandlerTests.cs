using AutoFixture;
using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Product", "Delete")]
public class DeleteProductHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly DeleteProduct.Command Command = new(new Fixture().Create<Guid>());	// Id


	[Theory(DisplayName = "Delete existing Product succeeds")]
	[AutoDomainData(true)]
	public async Task DeleteExistingProductSucceeds(Domain.Model.Entities.Product product)
	{
		var pictures = product.Pictures
							  .Union(product.Pictures
											.Select(x => x.Thumbnail!))
							  .ToArray();
		
		_dbContextMock.CreateAndSetupDbSetMock(product, out var productDbSetMock)
					  .CreateAndSetupDbSetMock(pictures);

		var command = Command with
					  {
						  Id = product.Id
					  };

		var sut = new DeleteProduct.Handler(_dbContextMock.Object);
		var result = await sut.Handle(command, CancellationToken.None);

		productDbSetMock.Verify(x => x.Remove(It.IsAny<Domain.Model.Entities.Product>()),
								Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()),
							  Times.Once);

		result.Should()
			  .BeEquivalentTo(CommandResult.Success());
	}
}