using ExifLibrary;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Application.DTOs;
using Monaco.Template.Backend.Common.BlobStorage;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;
using Monaco.Template.Backend.Domain.Model.Entities;
using SkiaSharp;
using File = Monaco.Template.Backend.Domain.Model.Entities.File;
using Image = Monaco.Template.Backend.Domain.Model.Entities.Image;

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
		var docId = await _blobStorageService.UploadFileAsync(stream,
															  fileName,
															  contentType,
															  cancellationToken: cancellationToken);

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
			await _blobStorageService.DeleteAsync(docId, cancellationToken: cancellationToken);
			throw;
		}
	}

	public async Task<Image> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
	{
		using var image = SKImage.FromEncodedData(stream);
		using var thumb = GetThumbnail(image, ThumbnailWidth, ThumbnailHeight);
		using var data = thumb.Encode();
		await using var thumbStream = data.AsStream();
		var metadata = GetMetadata(stream);
		var dateTaken = metadata.Get<ExifDateTime>(ExifTag.DateTimeOriginal)?.Value;
		(float latitude, float longitude)? gpsPosition = metadata.Contains(ExifTag.GPSLatitude) && metadata.Contains(ExifTag.GPSLongitude)
															 ? (metadata.Get<GPSLatitudeLongitude>(ExifTag.GPSLatitude).ToFloat(), metadata.Get<GPSLatitudeLongitude>(ExifTag.GPSLongitude).ToFloat())
															 : null;
		stream.Position = 0; // Reset streams position to read from beginning
		thumbStream.Position = 0;
		var imageIds = await Task.WhenAll(_blobStorageService.UploadFileAsync(stream,
																			  fileName,
																			  contentType,
																			  cancellationToken: cancellationToken),
										  _blobStorageService.UploadFileAsync(thumbStream,
																			  fileName,
																			  contentType,
																			  cancellationToken: cancellationToken));
		try
		{
			return CreateImage(imageIds[0],
							   Path.GetFileNameWithoutExtension(fileName),
							   Path.GetExtension(fileName),
							   contentType,
							   stream.Length,
							   (image.Width, image.Height),
							   dateTaken,
							   gpsPosition.HasValue
								   ? (gpsPosition.Value.latitude, gpsPosition.Value.longitude)
								   : null,
							   (imageIds[1],
								thumbStream.Length,
								(thumb.Width, thumb.Height)));
		}
		catch
		{
			await Task.WhenAll(_blobStorageService.DeleteAsync(imageIds[0], cancellationToken: cancellationToken),
							   _blobStorageService.DeleteAsync(imageIds[1], cancellationToken: cancellationToken));
			throw;
		}
	}

	public async Task<FileDownloadDto> DownloadFileAsync(File item, CancellationToken cancellationToken = default)
	{
		var file = await _blobStorageService.DownloadAsync(item.Id, cancellationToken: cancellationToken);

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
		_blobStorageService.DeleteAsync(file.Id, cancellationToken: cancellationToken);

	public Task DeleteImageAsync(Image image, CancellationToken cancellationToken) =>
		Task.WhenAll(_blobStorageService.DeleteAsync(image.Id, cancellationToken: cancellationToken),
					 image.ThumbnailId.HasValue
						 ? _blobStorageService.DeleteAsync(image.ThumbnailId.Value, cancellationToken: cancellationToken)
						 : Task.CompletedTask);

	public Task DeleteDocumentsAsync(Document[] documents, CancellationToken cancellationToken) =>
		Task.WhenAll(documents.Select(d => DeleteDocumentAsync(d, cancellationToken)));

	public Task DeleteImagesAsync(Image[] images, CancellationToken cancellationToken) =>
		Task.WhenAll(images.Select(img => DeleteImageAsync(img, cancellationToken)));

	public Task DeleteFilesAsync(File[] files, CancellationToken cancellationToken) =>
		Task.WhenAll(files.Select(f => DeleteFileAsync(f, cancellationToken)));

	public async Task<File> CopyFileAsync(File file, CancellationToken cancellationToken)
	{
		var copyId = await _blobStorageService.CopyAsync(file.Id, cancellationToken: cancellationToken);

		return file switch
			   {
				   Image image => CreateImage(copyId,
											  image.Name,
											  image.Extension,
											  image.ContentType,
											  image.Size,
											  (image.Dimensions.Width,
											   image.Dimensions.Height),
											  image.DateTaken,
											  image.Position is not null
												  ? (image.Position.Latitude,
													 image.Position.Longitude)
												  : null,
											  (await _blobStorageService.CopyAsync(image.Thumbnail!.Id,
																				   cancellationToken: cancellationToken),
											   image.Thumbnail.Size,
											   (image.Thumbnail.Dimensions.Width,
												image.Thumbnail.Dimensions.Height))),
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
													 SKSamplingOptions.Default);
		return SKImage.FromBitmap(scaledBitmap);
	}

	private static Image CreateImage(Guid id,
									 string name,
									 string extension,
									 string contentType,
									 long size,
									 (int Height, int Width) dimensions,
									 DateTime? dateTaken,
									 (float latitude, float longitude)? gpsPosition,
									 (Guid id,
									  long size,
									  (int height, int width) dimensions) thumbnail) =>
		new(id,
			name,
			extension,
			size,
			contentType,
			true,
			dimensions,
			dateTaken,
			gpsPosition,
			(thumbnail.id,
			 thumbnail.size,
			 thumbnail.dimensions));

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