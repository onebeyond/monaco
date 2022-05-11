using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using DcslGs.Template.Application.Services.Contracts;
using DcslGs.Template.Common.BlobStorage;
using DcslGs.Template.Common.BlobStorage.Contracts;
using DcslGs.Template.Domain.Model;
using DcslGs.Template.Application.Infrastructure.Context;
using File = DcslGs.Template.Domain.Model.File;
using Image = DcslGs.Template.Domain.Model.Image;

namespace DcslGs.Template.Application.Services;

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
        var image = System.Drawing.Image.FromStream(stream);
        var dateTaken = GetDateTaken(image);
        var thumb = GetThumbnail(image, ThumbnailWidth, ThumbnailHeight);
        await using var thumbStream = new MemoryStream();
        thumb.Save(thumbStream, image.RawFormat);   //Set the same ImageFormat as the original image

        stream.Position = 0;    //Reset stream position to read from beginning
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
                                   image.Size,
                                   thumb.Size,
                                   dateTaken,
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
            case Image image: await MakePermanentPicture(image, cancellationToken);
                break;
            case Document document: await MakePermanentDocument(document, cancellationToken);
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

    public DateTime? GetDateTaken(System.Drawing.Image image)
    {
        if (!image.PropertyIdList.Contains(36867))  //PropertyId=36867 is DateTaken property.
            return null;

        var dateTakenProp = image.GetPropertyItem(36867);
        //Original DateTaken format separates the Date part with ":", so we need to replace them by "-" to properly parse it
        return dateTakenProp is not null ? DateTime.Parse(new Regex(":").Replace(Encoding.UTF8.GetString(dateTakenProp.Value!), "-", 2)) : null;
    }

    public System.Drawing.Image GetThumbnail(System.Drawing.Image image, int thumbnailWidth, int thumbnailHeight)
    {
        //Calculates the proper scale to shrink the image so the aspect ratio remains the same for the thumbnail as well
        var scale = Math.Min(thumbnailWidth / (float)image.Height,
                             thumbnailHeight / (float)image.Width);
        if (scale > 1)  //If scale is bigger than 1, it means that the thumbnail would end up being bigger than the original image
            scale = 1;  //So we reset it to 1 so both image and thumbnail are the same size at worst.
        //Finally, we use the scale to calculate the final width and height to use for the thumbnail
        return image.GetThumbnailImage((int)(image.Width * scale),
                                       (int)(image.Height * scale),
                                       () => true,
                                       IntPtr.Zero);
    }

    private async Task<File> GetFile(Guid id, CancellationToken cancellationToken)
{
        return (await _dbContext.Set<File>().FindAsync(new object?[] { id }, cancellationToken))!;
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
                                    dateTaken)
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