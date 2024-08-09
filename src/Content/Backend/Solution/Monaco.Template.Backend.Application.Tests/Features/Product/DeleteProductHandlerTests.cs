using AutoFixture;
using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Model;
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
	private readonly Mock<IFileService> _fileServiceMock = new();
	private static readonly DeleteProduct.Command Command = new(new Fixture().Create<Guid>());	// Id


	[Theory(DisplayName = "Delete existing Product succeeds")]
	[AutoDomainData(true)]
	public async Task DeleteExistingProductSucceeds(Domain.Model.Product product)
	{
		var pictures = product.Pictures
							  .Union(product.Pictures
											.Select(x => x.Thumbnail!))
							  .ToArray();
		_dbContextMock.CreateAndSetupDbSetMock(product, out var productDbSetMock)
					  .CreateAndSetupDbSetMock(pictures, out var imageDbSetMock);

		var command = Command with
					  {
						  Id = product.Id
					  };

		var sut = new DeleteProduct.Handler(_dbContextMock.Object, _fileServiceMock.Object);
		var result = await sut.Handle(command, new CancellationToken());

		productDbSetMock.Verify(x => x.Remove(It.IsAny<Domain.Model.Product>()),
								Times.Once);
		imageDbSetMock.Verify(x => x.RemoveRange(It.IsAny<IEnumerable<Image>>()),
							  Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()),
							  Times.Once);
		_fileServiceMock.Verify(x => x.DeleteImagesAsync(It.IsAny<Image[]>(), It.IsAny<CancellationToken>()),
								Times.Once);

		result.ValidationResult
			  .IsValid
			  .Should()
			  .BeTrue();
		result.ItemNotFound
			  .Should()
			  .BeFalse();
	}
}