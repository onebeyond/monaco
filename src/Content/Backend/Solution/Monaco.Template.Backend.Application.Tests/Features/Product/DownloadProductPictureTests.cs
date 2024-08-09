using FluentAssertions;
using Microsoft.Extensions.Primitives;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Application.DTOs;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Queries - Product", "Download Product Picture")]
public class DownloadProductPictureTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private readonly Mock<IFileService> _fileServiceMock = new();

	[Theory(DisplayName = "Get existing product picture succeeds")]
	[AutoDomainData(true)]
	public async Task GetExistingProductPictureSucceeds(List<Domain.Model.Product> products, string contentType)
	{
		_dbContextMock.CreateAndSetupDbSetMock(products);

		var product = products.First();
		var picture = product.Pictures.First();
		var pictureFileName = $"{picture.Name}{picture.Extension}";

		_fileServiceMock.Setup(x => x.DownloadFileAsync(It.IsAny<Domain.Model.File>(), 
														It.IsAny<CancellationToken>()))
						.ReturnsAsync(new FileDownloadDto(new MemoryStream(),
														  pictureFileName,
														  contentType));

		var query = new DownloadProductPicture.Query(product.Id,
													 picture.Id,
													 []);

		var sut = new DownloadProductPicture.Handler(_dbContextMock.Object, _fileServiceMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .NotBeNull();
		result!.FileName
			   .Should()
			   .Be(pictureFileName);
	}

	[Theory(DisplayName = "Get existing product picture thumbnail succeeds")]
	[AutoDomainData(true)]
	public async Task GetExistingProductPictureThumbnailSucceeds(Domain.Model.Product[] products, string contentType)
	{
		_dbContextMock.CreateAndSetupDbSetMock(products);

		var product = products.First();
		var picture = product.Pictures.First();
		var pictureFileName = $"{picture.Name}{picture.Extension}";

		_fileServiceMock.Setup(x => x.DownloadFileAsync(It.IsAny<Domain.Model.File>(), It.IsAny<CancellationToken>()))
						.ReturnsAsync(new FileDownloadDto(new MemoryStream(),
														  pictureFileName,
														  contentType));

		var query = new DownloadProductPicture.Query(product.Id,
													 picture.Id,
													 [new("thumbnail", "true")]);

		var sut = new DownloadProductPicture.Handler(_dbContextMock.Object, _fileServiceMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .NotBeNull();
		result!.FileName
			   .Should()
			   .Be(pictureFileName);
	}

	[Theory(DisplayName = "Get non-existing product picture fails")]
	[AutoDomainData(true)]
	public async Task GetNonExistingProductByIdFails(List<Domain.Model.Product> products)
	{
		_dbContextMock.CreateAndSetupDbSetMock(products);
		var query = new DownloadProductPicture.Query(Guid.NewGuid(),
													 Guid.NewGuid(),
													 []);

		var sut = new DownloadProductPicture.Handler(_dbContextMock.Object, _fileServiceMock.Object);
		var result = await sut.Handle(query, new CancellationToken());

		result.Should()
			  .BeNull();
	}
}