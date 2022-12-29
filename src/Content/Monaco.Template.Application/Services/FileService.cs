using ExifLibrary;
using Monaco.Template.Application.Infrastructure.Context;
using Monaco.Template.Application.Services.Contracts;
using Monaco.Template.Common.BlobStorage;
using Monaco.Template.Common.BlobStorage.Contracts;
using Monaco.Template.Domain.Model;
using SkiaSharp;
using System.Drawing;
using File = Monaco.Template.Domain.Model.File;
using Image = Monaco.Template.Domain.Model.Image;

namespace Monaco.Template.Application.Services;

public class FileService : IFileService
{
	private readonly AppDbContext _dbContext;
	private readonly IBlobStorageService _blobStorageService;

	private const int ThumbnailWidth = 120;
	private const int ThumbnailHeight = 120;

	public FileService(AppDbContext dbContext, IBlobStorageService blobStorageService)
	{
		_dbContext = dbContext;
		_blobStorageService = blobStorageService;
	}

	public async Task<File> Upload(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
	{
		var fileType = _blobStorageService.GetFileType(Path.GetExtension(fileName));

		return fileType switch
			   {
				   FileTypeEnum.Image => await UploadImage(stream, fileName, contentType, cancellationToken),
				   _ => await UploadDocument(stream, fileName, contentType, cancellationToken),
			   };
	}

	public async Task<Document> UploadDocument(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
	{
		var docId = await _blobStorageService.UploadTempFileAsync(stream, fileName, contentType, cancellationToken);

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
			await _blobStorageService.DeleteAsync(docId, true, cancellationToken);
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
		stream.Position = 0; //Reset streams position to read from beginning
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
			return await SaveImage(imageIds[0],
								   imageIds[1],
								   Path.GetFileNameWithoutExtension(fileName),
								   Path.GetExtension(fileName),
								   contentType,
								   stream.Length,
								   thumbStream.Length,
								   new Size(image.Width, image.Height),
								   new Size(thumb.Width, thumb.Height),
								   dateTaken,
								   gpsLat,
								   gpsLong,
								   cancellationToken);
		}
		catch
		{
			await Task.WhenAll(_blobStorageService.DeleteAsync(imageIds[0], true, cancellationToken),
							   _blobStorageService.DeleteAsync(imageIds[1], true, cancellationToken));
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
		await _dbContext.SaveEntitiesAsync(cancellationToken);

		await _blobStorageService.MakePermanentAsync(file.Id, cancellationToken);
		if (file.ThumbnailId.HasValue)
			await _blobStorageService.MakePermanentAsync(file.ThumbnailId.Value, cancellationToken);
	}

	public async Task MakePermanentDocument(Document file, CancellationToken cancellationToken)
	{
		file.MakePermanent();
		await _dbContext.SaveEntitiesAsync(cancellationToken);

		await _blobStorageService.MakePermanentAsync(file.Id, cancellationToken);
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
		_dbContext.Set<Document>().Remove(file);
		await _dbContext.SaveEntitiesAsync(cancellationToken);

		await _blobStorageService.DeleteAsync(file.Id, file.IsTemp, cancellationToken);
	}

	public async Task DeleteImage(Image file, CancellationToken cancellationToken)
	{
		var thumbId = file.ThumbnailId;

		if (file.Thumbnail != null)
			_dbContext.Set<Image>().Remove(file.Thumbnail);
		_dbContext.Set<Image>().Remove(file);
		await _dbContext.SaveEntitiesAsync(cancellationToken);

		await Task.WhenAll(_blobStorageService.DeleteAsync(file.Id, file.IsTemp, cancellationToken),
						   thumbId.HasValue
							   ? _blobStorageService.DeleteAsync(thumbId.Value, file.IsTemp, cancellationToken)
							   : Task.CompletedTask);
	}

	public async Task<File> CopyFile(Guid id, CancellationToken cancellationToken)
	{
		var file = await GetFile(id, cancellationToken);
		var copyId = await _blobStorageService.CopyAsync(id, file.IsTemp, cancellationToken);

		switch (file)
		{
			case Image image:
				Guid? thumbCopyId = null;
				if (image.ThumbnailId.HasValue)
					thumbCopyId = image.ThumbnailId.HasValue ? await _blobStorageService.CopyAsync(image.ThumbnailId.Value, image.IsTemp, cancellationToken) : null;
				return await SaveImage(copyId,
									   thumbCopyId,
									   image.Name,
									   image.Extension,
									   image.ContentType,
									   image.Size,
									   image.Thumbnail?.Size,
									   new Size(image.Width, image.Height),
									   image.ThumbnailId.HasValue ? new Size(image.Thumbnail!.Width, image.Thumbnail.Height) : null,
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
		//Calculates the proper scale to shrink the image so the aspect ratio remains the same for the thumbnail as well
		var scale = Math.Min(thumbnailWidth / (float)image.Height,
							 thumbnailHeight / (float)image.Width);
		if (scale > 1) //If scale is bigger than 1, it means that the thumbnail would end up being bigger than the original image
			scale = 1; //So we reset it to 1 so both image and thumbnail are the same size at worst.
		//Finally, we use the scale to calculate the final width and height to use for the thumbnail
		var sourceBitmap = SKBitmap.FromImage(image);
		using var scaledBitmap = sourceBitmap.Resize(new SKImageInfo((int)(image.Width * scale),
																	 (int)(image.Height * scale)),
													 SKFilterQuality.Medium);
		return SKImage.FromBitmap(scaledBitmap);
	}

	private async Task<File> GetFile(Guid id, CancellationToken cancellationToken)
	{
		return (await _dbContext.Set<File>().FindAsync(new object[] { id }, cancellationToken))!;
	}

	private async Task<Image> SaveImage(Guid imageId,
										Guid? thumbnailId,
										string name,
										string extension,
										string contentType,
										long imageSize,
										long? thumbSize,
										Size imageDimentions,
										Size? thumbDimentions,
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
									thumbDimentions!.Value.Height,
									thumbDimentions!.Value.Width,
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
							 imageDimentions.Height,
							 imageDimentions.Width,
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
		await _dbContext.Set<T>().AddAsync(item, cancellationToken);
		await _dbContext.SaveEntitiesAsync(cancellationToken);

		return item;
	}
}