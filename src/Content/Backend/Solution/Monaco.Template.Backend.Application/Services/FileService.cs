using ExifLibrary;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Application.DTOs;
using Monaco.Template.Backend.Common.BlobStorage;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;
using Monaco.Template.Backend.Domain.Model;
using SkiaSharp;
using File = Monaco.Template.Backend.Domain.Model.File;
using Image = Monaco.Template.Backend.Domain.Model.Image;

namespace Monaco.Template.Backend.Application.Services;

internal class FileService : IFileService
{
	private readonly IBlobStorageService _blobStorageService;

	private const int ThumbnailWidth = 120;
	private const int ThumbnailHeight = 120;

	public FileService(IBlobStorageService blobStorageService)
	{
		_blobStorageService = blobStorageService;
	}

	public async Task<File> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
	{
		var fileType = _blobStorageService.GetFileType(Path.GetExtension(fileName));

		return fileType switch
			   {
				   FileTypeEnum.Image => await UploadImageAsync(stream, fileName, contentType, cancellationToken),
				   _ => await UploadDocumentAsync(stream, fileName, contentType, cancellationToken),
			   };
	}

	public async Task<Document> UploadDocumentAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
	{
		var docId = await _blobStorageService.UploadTempFileAsync(stream,
																  fileName,
																  contentType,
																  cancellationToken);

		try
		{
			return CreateDocument(docId,
								  Path.GetFileNameWithoutExtension(fileName),
								  Path.GetExtension(fileName),
								  contentType,
								  stream.Length);
		}
		catch
		{
			await _blobStorageService.DeleteAsync(docId, true, cancellationToken);
			throw;
		}
	}

	public async Task<Image> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
	{
		using var image = SKImage.FromEncodedData(stream);
		using var thumb = GetThumbnail(image, ThumbnailWidth, ThumbnailHeight);
		using var data = thumb.Encode();
		await using var thumbStream = data.AsStream();
		var metadata = GetMetadata(stream);
		var dateTaken = metadata.Get<ExifDateTime>(ExifTag.DateTimeOriginal)?.Value;
		var gpsLat = metadata.Get<GPSLatitudeLongitude>(ExifTag.GPSLatitude)?.ToFloat();
		var gpsLong = metadata.Get<GPSLatitudeLongitude>(ExifTag.GPSLongitude)?.ToFloat();
		stream.Position = 0; // Reset streams position to read from beginning
		thumbStream.Position = 0;
		var imageIds = await Task.WhenAll(_blobStorageService.UploadTempFileAsync(stream,
																				  fileName,
																				  contentType,
																				  cancellationToken),
										  _blobStorageService.UploadTempFileAsync(thumbStream,
																				  fileName,
																				  contentType,
																				  cancellationToken));
		try
		{
			return CreateImage(imageIds[0],
							   imageIds[1],
							   Path.GetFileNameWithoutExtension(fileName),
							   Path.GetExtension(fileName),
							   contentType,
							   stream.Length,
							   thumbStream.Length,
							   (image.Width, image.Height),
							   (thumb.Width, thumb.Height),
							   dateTaken,
							   gpsLat,
							   gpsLong);
		}
		catch
		{
			await Task.WhenAll(_blobStorageService.DeleteAsync(imageIds[0], true, cancellationToken),
							   _blobStorageService.DeleteAsync(imageIds[1], true, cancellationToken));
			throw;
		}
	}

	public Task MakePermanentFileAsync(File file, CancellationToken cancellationToken) =>
		file switch
		{
			Image image => MakePermanentImageAsync(image, cancellationToken),
			Document document => MakePermanentDocumentAsync(document, cancellationToken),
			_ => throw new NotImplementedException()
		};

	public Task MakePermanentFilesAsync(File[] files, CancellationToken cancellationToken) =>
		Task.WhenAll(files.Select(f => MakePermanentFileAsync(f, cancellationToken)));

	public Task MakePermanentImageAsync(Image image, CancellationToken cancellationToken) =>
		Task.WhenAll(_blobStorageService.MakePermanentAsync(image.Id, cancellationToken),
					 image.ThumbnailId.HasValue
						 ? _blobStorageService.MakePermanentAsync(image.ThumbnailId.Value, cancellationToken)
						 : Task.CompletedTask);

	public Task MakePermanentImagesAsync(Image[] images, CancellationToken cancellationToken) =>
		Task.WhenAll(images.Select(img => MakePermanentImageAsync(img, cancellationToken)));

	public Task MakePermanentDocumentAsync(Document document, CancellationToken cancellationToken) =>
		_blobStorageService.MakePermanentAsync(document.Id, cancellationToken);

	public Task MakePermanentDocumentsAsync(Document[] documents, CancellationToken cancellationToken) =>
		Task.WhenAll(documents.Select(doc => MakePermanentDocumentAsync(doc, cancellationToken)));

	public async Task<FileDownloadDto> DownloadFileAsync(File item, CancellationToken cancellationToken)
	{
		var file = await _blobStorageService.DownloadAsync(item.Id, item.IsTemp, cancellationToken);

		return new(file,
				   $"{item.Name}{item.Extension}",
				   item.ContentType);
	}
	
	public Task DeleteFileAsync(File file, CancellationToken cancellationToken) =>
		file switch
		{
			Image image => DeleteImageAsync(image, cancellationToken),
			Document document => DeleteDocumentAsync(document, cancellationToken),
			_ => throw new NotImplementedException()
		};

	public Task DeleteDocumentAsync(Document file, CancellationToken cancellationToken) =>
		_blobStorageService.DeleteAsync(file.Id, file.IsTemp, cancellationToken);

	public Task DeleteImageAsync(Image image, CancellationToken cancellationToken) =>
		Task.WhenAll(_blobStorageService.DeleteAsync(image.Id, image.IsTemp, cancellationToken),
					 image.ThumbnailId.HasValue
						 ? _blobStorageService.DeleteAsync(image.ThumbnailId.Value, image.IsTemp, cancellationToken)
						 : Task.CompletedTask);

	public Task DeleteDocumentsAsync(Document[] documents, CancellationToken cancellationToken) =>
		Task.WhenAll(documents.Select(d => DeleteDocumentAsync(d, cancellationToken)));

	public Task DeleteImagesAsync(Image[] images, CancellationToken cancellationToken) =>
		Task.WhenAll(images.Select(img => DeleteImageAsync(img, cancellationToken)));

	public Task DeleteFilesAsync(File[] files, CancellationToken cancellationToken) =>
		Task.WhenAll(files.Select(f => DeleteFileAsync(f, cancellationToken)));

	public async Task<File> CopyFileAsync(File file, CancellationToken cancellationToken)
	{
		var copyId = await _blobStorageService.CopyAsync(file.Id, file.IsTemp, cancellationToken);

		return file switch
			   {
				   Image image => CreateImage(copyId,
											  image.ThumbnailId.HasValue
												  ? await _blobStorageService.CopyAsync(image.ThumbnailId.Value,
																						image.IsTemp,
																						cancellationToken)
												  : null,
											  image.Name,
											  image.Extension,
											  image.ContentType,
											  image.Size,
											  image.Thumbnail?.Size,
											  (image.Dimensions.Width,
											   image.Dimensions.Height),
											  image.ThumbnailId.HasValue
												  ? (image.Thumbnail!.Dimensions.Width,
													 image.Thumbnail.Dimensions.Height)
												  : null,
											  image.DateTaken,
											  image.Position?.Latitude,
											  image.Position?.Longitude),
				   _ => CreateDocument(copyId,
									   file.Name,
									   file.Extension,
									   file.ContentType,
									   file.Size)
			   };
	}

	public ExifPropertyCollection<ExifProperty> GetMetadata(Stream stream)
	{
		stream.Position = 0;
		return ImageFile.FromStream(stream).Properties;
	}

	public SKImage GetThumbnail(SKImage image, int thumbnailWidth, int thumbnailHeight)
	{
		// Calculates the proper scale to shrink the image so the aspect ratio remains the same for the thumbnail as well
		var scale = Math.Min(thumbnailWidth / (float)image.Height,
							 thumbnailHeight / (float)image.Width);
		if (scale > 1) // If scale is bigger than 1, it means that the thumbnail would end up being bigger than the original image
			scale = 1; // So we reset it to 1 so both image and thumbnail are the same size at worst.
					   // Finally, we use the scale to calculate the final width and height to use for the thumbnail
		var sourceBitmap = SKBitmap.FromImage(image);
		using var scaledBitmap = sourceBitmap.Resize(new SKImageInfo((int)(image.Width * scale),
																	 (int)(image.Height * scale)),
													 SKFilterQuality.Medium);
		return SKImage.FromBitmap(scaledBitmap);
	}

	private static Image CreateImage(Guid imageId,
									 Guid? thumbnailId,
									 string name,
									 string extension,
									 string contentType,
									 long imageSize,
									 long? thumbSize,
									 (int Height, int Width) imageDimensions,
									 (int Height, int Width)? thumbDimensions,
									 DateTime? dateTaken,
									 float? gpsLatitude,
									 float? gpsLongitude) =>
		new(imageId,
			name,
			extension,
			imageSize,
			contentType,
			true,
			imageDimensions.Height,
			imageDimensions.Width,
			dateTaken,
			gpsLatitude,
			gpsLongitude,
			thumbnailId.HasValue
				? new Image(thumbnailId.Value,
							name,
							extension,
							thumbSize!.Value,
							contentType,
							true,
							thumbDimensions!.Value.Height,
							thumbDimensions!.Value.Width,
							dateTaken,
							gpsLatitude,
							gpsLongitude)
				: null);

	private static Document CreateDocument(Guid id,
										   string name,
										   string extension,
										   string contentType,
										   long size) =>
		new(id,
			name,
			extension,
			size,
			contentType,
			true);
}