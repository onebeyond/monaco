using DcslGs.Template.Domain.Model;
using File = DcslGs.Template.Domain.Model.File;

namespace DcslGs.Template.Infrastructure.Services.Contracts;

public interface IFileService
{
    Task<File> Upload(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
    Task<Document> UploadDocument(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
    Task<Image> UploadImage(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
    Task MakePermanent(Guid id, CancellationToken cancellationToken);
    Task MakePermanentPicture(Image file, CancellationToken cancellationToken);
    Task MakePermanentDocument(Document file, CancellationToken cancellationToken);
    Task Delete(Guid id, CancellationToken cancellationToken);
    Task<File> CopyFile(Guid id, CancellationToken cancellationToken);
    DateTime? GetDateTaken(System.Drawing.Image image);
    System.Drawing.Image GetThumbnail(System.Drawing.Image image, int thumbnailWidth, int thumbnailHeight);

}