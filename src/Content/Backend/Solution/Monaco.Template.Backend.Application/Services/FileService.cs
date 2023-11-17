using ExifLibrary;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.BlobStorage;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;
using Monaco.Template.Backend.Domain.Model;
using SkiaSharp;
using File = Monaco.Template.Backend.Domain.Model.File;
using Image = Monaco.Template.Backend.Domain.Model.Image;

namespace Monaco.Template.Backend.Application.Services;

public class FileService(AppDbContext dbContext, IBlobStorageService blobStorageService) : IFileService
{
	private const int ThumbnailWidth = 120;
	private const int ThumbnailHeight = 120;

	public async Task<File> Upload(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
	{
		var fileType = blobStorageService.GetFileType(Path.GetExtension(fileName));

		return fileType switch
			   {
				   FileTypeEnum.Image => await UploadImage(stream, fileName, contentType, cancellationToken),
				   _ => await UploadDocument(stream, fileName, contentType, cancellationToken),
			   };
	}

	public async Task<Document> UploadDocument(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
	{
		var docId = await blobStorageService.UploadTempFileAsync(stream, fileName, contentType, cancellationToken);

		try
		{
			return await SaveDocument(docId,
									  Path.GetFileNameWithoutExtension(fileName),
									  Path.GetExtension(fileName),
									  contentType,
									  stream.Length,
									  cancellationToken);
		}
		catch
		{
			await blobStorageService.DeleteAsync(docId, true, cancellationToken);
			throw;
		}
	}

	public async Task<Image> UploadImage(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
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
		var imageIds = await Task.WhenAll(blobStorageService.UploadTempFileAsync(stream,
																				  fileName,
																				  contentType,
																				  cancellationToken),
										  blobStorageService.UploadTempFileAsync(thumbStream,
																				  fileName,
																				  contentType,
																				  cancellationToken));
		try
		{
			return await SaveImage(imageIds[0],
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
								   gpsLong,
								   cancellationToken);
		}
		catch
		{
			await Task.WhenAll(blobStorageService.DeleteAsync(imageIds[0], true, cancellationToken),
							   blobStorageService.DeleteAsync(imageIds[1], true, cancellationToken));
			throw;
		}
	}

	public async Task MakePermanent(Guid id, CancellationToken cancellationToken)
	{
		var file = await GetFile(id, cancellationToken);
		switch (file)
		{
			case Image image:
				await MakePermanentPicture(image, cancellationToken);
				break;
			case Document document:
				await MakePermanentDocument(document, cancellationToken);
				break;
		}
	}

	public async Task MakePermanentPicture(Image file, CancellationToken cancellationToken)
	{
		file.MakePermanent();
		file.Thumbnail?.MakePermanent();
		await dbContext.SaveEntitiesAsync(cancellationToken);

		await blobStorageService.MakePermanentAsync(file.Id, cancellationToken);
		if (file.ThumbnailId.HasValue)
			await blobStorageService.MakePermanentAsync(file.ThumbnailId.Value, cancellationToken);
	}

	public async Task MakePermanentDocument(Document file, CancellationToken cancellationToken)
	{
		file.MakePermanent();
		await dbContext.SaveEntitiesAsync(cancellationToken);

		await blobStorageService.MakePermanentAsync(file.Id, cancellationToken);
	}

	public async Task Delete(Guid id, CancellationToken cancellationToken)
	{
		var file = await GetFile(id, cancellationToken);
		switch (file)
		{
			case Image image:
				await DeleteImage(image, cancellationToken);
				break;
			case Document document:
				await DeleteDocument(document, cancellationToken);
				break;
		}
	}

	public async Task DeleteDocument(Document file, CancellationToken cancellationToken)
	{
		dbContext.Set<Document>().Remove(file);
		await dbContext.SaveEntitiesAsync(cancellationToken);

		await blobStorageService.DeleteAsync(file.Id, file.IsTemp, cancellationToken);
	}

	public async Task DeleteImage(Image file, CancellationToken cancellationToken)
	{
		var thumbId = file.ThumbnailId;

		if (file.Thumbnail != null)
			dbContext.Set<Image>().Remove(file.Thumbnail);
		dbContext.Set<Image>().Remove(file);
		await dbContext.SaveEntitiesAsync(cancellationToken);

		await Task.WhenAll(blobStorageService.DeleteAsync(file.Id, file.IsTemp, cancellationToken),
						   thumbId.HasValue
							   ? blobStorageService.DeleteAsync(thumbId.Value, file.IsTemp, cancellationToken)
							   : Task.CompletedTask);
	}

	public async Task<File> CopyFile(Guid id, CancellationToken cancellationToken)
	{
		var file = await GetFile(id, cancellationToken);
		var copyId = await blobStorageService.CopyAsync(id, file.IsTemp, cancellationToken);

		switch (file)
		{
			case Image image:
				Guid? thumbCopyId = image.ThumbnailId.HasValue
						? await blobStorageService.CopyAsync(image.ThumbnailId.Value, image.IsTemp, cancellationToken)
						: null;

				return await SaveImage(copyId,
									   thumbCopyId,
									   image.Name,
									   image.Extension,
									   image.ContentType,
									   image.Size,
									   image.Thumbnail?.Size,
									   (image.Width, image.Height),
									   image.ThumbnailId.HasValue ? (image.Thumbnail!.Width, image.Thumbnail.Height) : null,
									   image.DateTaken,
									   image.GpsLatitude,
									   image.GpsLongitude,
									   cancellationToken);

			default:
				return await SaveDocument(copyId,
										  file.Name,
										  file.Extension,
										  file.ContentType,
										  file.Size,
										  cancellationToken);
		}
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

	private async Task<File> GetFile(Guid id, CancellationToken cancellationToken)
	{
		return (await dbContext.Set<File>().FindAsync(new object[] { id }, cancellationToken))!;
	}

	private async Task<Image> SaveImage(Guid imageId,
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
										float? gpsLongitude,
										CancellationToken cancellationToken)
	{
		var thumb = thumbnailId.HasValue
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
						: null;

		var item = new Image(imageId,
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
							 thumb);

		return await Save(item, cancellationToken);
	}

	private async Task<Document> SaveDocument(Guid id,
											  string name,
											  string extension,
											  string contentType,
											  long size,
											  CancellationToken cancellationToken)
	{
		var item = new Document(id,
								name,
								extension,
								size,
								contentType,
								true);

		return await Save(item, cancellationToken);
	}

	private async Task<T> Save<T>(T item, CancellationToken cancellationToken) where T : File
	{
		await dbContext.Set<T>().AddAsync(item, cancellationToken);
		await dbContext.SaveEntitiesAsync(cancellationToken);

		return item;
	}
}