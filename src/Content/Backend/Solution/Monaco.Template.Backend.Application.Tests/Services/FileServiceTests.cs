using ExifLibrary;
using FluentAssertions;
using Monaco.Template.Backend.Application.Services;
using Monaco.Template.Backend.Common.BlobStorage;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using SkiaSharp;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using File = Monaco.Template.Backend.Domain.Model.Entities.File;

namespace Monaco.Template.Backend.Application.Tests.Services;

[ExcludeFromCodeCoverage]
[Trait("Application Services", "File Service")]
public class FileServiceTests
{
	private readonly Mock<IBlobStorageService> _blobStorageServiceMock = new();
	private const string TxtBase64 = "TW9uYWNvIFVuaXQgVGVzdCBGaWxlIGZvciBVcGxvYWQgZG9jdW1lbnQgc3VjY2VlZHMu";
	private const string ImgBase64 = "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";

	[Fact(DisplayName = "Upload image succeeds")]
	public async Task UploadImageSucceeds()
	{
		var sut = new FileService(_blobStorageServiceMock.Object);

		await using var stream = new MemoryStream(Convert.FromBase64String(ImgBase64));
		await sut.UploadImageAsync(stream, "sample-image.png", "image/png", CancellationToken.None);
		stream.Close();

		_blobStorageServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<CancellationToken>()),
									   Times.Exactly(2));
	}

	[Fact(DisplayName = "Uploading an image file uploads an image")]
	public async Task UploadingImageFileUploadsImage()
	{
		_blobStorageServiceMock.Setup(x => x.GetFileType(It.IsAny<string>()))
							   .Returns(FileTypeEnum.Image);

		var sut = new FileService(_blobStorageServiceMock.Object);

		await using var stream = new MemoryStream(Convert.FromBase64String(ImgBase64));
		await sut.UploadAsync(stream,
							  "sample-image.png",
							  "image/png",
							  CancellationToken.None);
		stream.Close();

		_blobStorageServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<CancellationToken>()),
									   Times.Exactly(2));
	}
	
	[Fact(DisplayName = "Upload document succeeds")]
	public async Task UploadDocumentSucceeds()
	{
		var sut = new FileService(_blobStorageServiceMock.Object);
		
		await using var stream = new MemoryStream(Convert.FromBase64String(TxtBase64));
		await sut.UploadDocumentAsync(stream,
									  "sample-text-file.txt",
									  "text/plain",
									  CancellationToken.None);
		stream.Close();

		_blobStorageServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<CancellationToken>()),
									   Times.Exactly(1));
	}

	[Fact(DisplayName = "Error while saving document deletes it from storage")]
	public async Task ErrorSavingDocumentDeletesIt()
	{
		var sut = new FileService(_blobStorageServiceMock.Object);

		var action = () => sut.UploadDocumentAsync(null!,
												   "sample-text-file.txt",
												   "text/plain",
												   CancellationToken.None);

		await action.Should()
					.ThrowAsync<Exception>();
		_blobStorageServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<CancellationToken>()),
									   Times.Once);
		_blobStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(),
														  It.IsAny<string>(),
														  It.IsAny<CancellationToken>()),
									   Times.Once);
	}

	[Fact(DisplayName = "Uploading a text file uploads a document")]
	public async Task UploadingTextFileUploadsDocument()
	{
		var sut = new FileService(_blobStorageServiceMock.Object);
		
		await using var stream = new MemoryStream(Convert.FromBase64String(TxtBase64));
		await sut.UploadAsync(stream, "sample-text-file.txt", "text/plain", CancellationToken.None);
		stream.Close();

		_blobStorageServiceMock.Verify(x => x.UploadFileAsync(It.IsAny<Stream>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<string>(),
															  It.IsAny<CancellationToken>()),
									   Times.Exactly(1));
	}

	[Theory(DisplayName = "Download Document succeeds")]
	[AutoDomainData]
	public async Task DownloadFileSucceeds(Document document)
	{
		_blobStorageServiceMock.Setup(x => x.DownloadAsync(It.IsAny<Guid>(),
														   It.IsAny<string>(),
														   It.IsAny<CancellationToken>()))
							   .ReturnsAsync(new MemoryStream());

		var sut = new FileService(_blobStorageServiceMock.Object);

		var result = await sut.DownloadFileAsync(document, It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.DownloadAsync(It.IsAny<Guid>(),
															It.IsAny<string>(),
															It.IsAny<CancellationToken>()),
									   Times.Once);
		result.FileName
			  .Should()
			  .Be($"{document.Name}{document.Extension}");
		result.ContentType
			  .Should()
			  .Be(document.ContentType);
	}
	
	[Theory(DisplayName = "Delete Document succeeds")]
	[AutoDomainData]
	public async Task DeleteDocumentSucceeds(Document document)
	{
		var sut = new FileService(_blobStorageServiceMock.Object);

		await sut.DeleteDocumentAsync(document, It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(),
														  It.IsAny<string>(),
														  It.IsAny<CancellationToken>()),
									   Times.Once);
	}

	[Theory(DisplayName = "Delete Documents succeeds")]
	[AutoDomainData]
	public async Task DeleteDocumentsSucceeds(Document[] documents)
	{
		var sut = new FileService(_blobStorageServiceMock.Object);

		await sut.DeleteDocumentsAsync(documents, It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(),
														  It.IsAny<string>(),
														  It.IsAny<CancellationToken>()),
									   Times.Exactly(documents.Length));
	}

	[Theory(DisplayName = "Delete Image succeeds")]
	[AutoDomainData(true)]
	public async Task DeleteImageSucceeds(Image image)
	{
		typeof(Image)
			 .GetProperty(nameof(Image.ThumbnailId))!
			 .SetValue(image, image.Thumbnail!.Id, null);

		var sut = new FileService(_blobStorageServiceMock.Object);

		await sut.DeleteImageAsync(image, It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(),
														  It.IsAny<string>(),
														  It.IsAny<CancellationToken>()),
									   Times.Exactly(2));
	}

	[Theory(DisplayName = "Delete Images succeeds")]
	[AutoDomainData(true)]
	public async Task DeleteImagesSucceeds(Image[] images)
	{
		foreach (var image in images)
			typeof(Image)
				.GetProperty(nameof(Image.ThumbnailId))!
				.SetValue(image, image.Thumbnail!.Id, null);

		var sut = new FileService(_blobStorageServiceMock.Object);

		await sut.DeleteImagesAsync(images, It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(),
														  It.IsAny<string>(),
														  It.IsAny<CancellationToken>()),
									   Times.Exactly(2 * images.Length));
	}

	[Theory(DisplayName = "Delete File Document succeeds")]
	[AutoDomainData]
	public async Task DeleteFileDocumentSucceeds(Document document)
	{
		var sut = new FileService(_blobStorageServiceMock.Object);

		await sut.DeleteFileAsync(document, It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(),
														  It.IsAny<string>(),
														  It.IsAny<CancellationToken>()),
									   Times.Once);
	}

	[Theory(DisplayName = "Delete File Image succeeds")]
	[AutoDomainData(true)]
	public async Task DeleteFileImageSucceeds(Image image)
	{
		typeof(Image).GetProperty(nameof(Image.ThumbnailId))!
					 .SetValue(image, image.Thumbnail!.Id, null);

		var sut = new FileService(_blobStorageServiceMock.Object);

		await sut.DeleteFileAsync(image, It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(),
														  It.IsAny<string>(),
														  It.IsAny<CancellationToken>()),
									   Times.Exactly(2));
	}

	[Fact(DisplayName = "Delete dummy file throws exception")]
	public async Task DeleteDummyFileThrows()
	{
		var sut = new FileService(_blobStorageServiceMock.Object);

		var action = () => sut.DeleteFileAsync(new DummyFile(), It.IsAny<CancellationToken>());

		await action.Should()
					.ThrowAsync<NotImplementedException>();
	}

	[Theory(DisplayName = "Delete Files succeeds")]
	[AutoDomainData(true)]
	public async Task DeleteFilesSucceeds(Document document, Image image)
	{
		typeof(Image)
			.GetProperty(nameof(Image.ThumbnailId))!
			.SetValue(image, image.Thumbnail!.Id, null);

		var sut = new FileService(_blobStorageServiceMock.Object);

		await sut.DeleteFilesAsync([document, image], It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.DeleteAsync(It.IsAny<Guid>(),
														  It.IsAny<string>(),
														  It.IsAny<CancellationToken>()),
									   Times.Exactly(3));
	}

	[Theory(DisplayName = "Copy document succeeds")]
	[AutoDomainData(true)]
	public async Task CopyDocumentSucceeds(Document document)
	{
		_blobStorageServiceMock.Setup(x => x.CopyAsync(It.IsAny<Guid>(),
													   It.IsAny<string>(),
													   It.IsAny<CancellationToken>()))
							   .ReturnsAsync(Guid.NewGuid());

		var sut = new FileService(_blobStorageServiceMock.Object);
		await sut.CopyFileAsync(document, It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.CopyAsync(It.IsAny<Guid>(),
														It.IsAny<string>(),
														It.IsAny<CancellationToken>()),
									   Times.Once);
	}

	[Theory(DisplayName = "Copy image succeeds")]
	[AutoDomainData(true)]
	public async Task CopyImageSucceeds(Image image)
	{
		typeof(Image)
			.GetProperty(nameof(Image.ThumbnailId))!
			.SetValue(image, image.Thumbnail!.Id, null);

		_blobStorageServiceMock.Setup(x => x.CopyAsync(It.IsAny<Guid>(),
													   It.IsAny<string>(),
													   It.IsAny<CancellationToken>()))
							   .ReturnsAsync(Guid.NewGuid());

		var sut = new FileService(_blobStorageServiceMock.Object);
		await sut.CopyFileAsync(image, It.IsAny<CancellationToken>());

		_blobStorageServiceMock.Verify(x => x.CopyAsync(It.IsAny<Guid>(),
														It.IsAny<string>(),
														It.IsAny<CancellationToken>()),
									   Times.Exactly(2));
	}

	[Fact(DisplayName = "Get Metadata succeeds")]
	public async Task GetMetadataSucceeds()
	{
		var sut = new FileService(_blobStorageServiceMock.Object);

		await using var stream = new MemoryStream(Convert.FromBase64String(ImgBase64));
		var result = sut.GetMetadata(stream);

		result.Count
			  .Should()
			  .Be(1);
		result.Should()
			  .Contain(property => property.Tag == ExifTag.PNGCreationTime);
	}

	[Fact(DisplayName = "Get Thumbnail succeeds")]
	public async Task GetThumbnailSucceeds()
	{
		var sut = new FileService(_blobStorageServiceMock.Object);

		await using var stream = new MemoryStream(Convert.FromBase64String(ImgBase64));
		using var image = SKImage.FromEncodedData(stream);
		var result = sut.GetThumbnail(image, 120, 120);

		result.Width
			  .Should()
			  .BeLessOrEqualTo(120);
		result.Height
			  .Should()
			  .BeLessOrEqualTo(120);
	}

	[Fact(DisplayName = "Get Thumbnail bigger than original keeps same size")]
	public async Task GetThumbnailBiggerThanOriginalKeepsSize()
	{
		var sut = new FileService(_blobStorageServiceMock.Object);

		await using var stream = new MemoryStream(Convert.FromBase64String(ImgBase64));
		using var image = SKImage.FromEncodedData(stream);
		var (width, height) = (image.Width, image.Height);
		var result = sut.GetThumbnail(image, 1000, 1000);

		result.Width
			  .Should()
			  .Be(width);
		result.Height
			  .Should()
			  .BeLessOrEqualTo(height);
	}
}

internal class DummyFile : File;