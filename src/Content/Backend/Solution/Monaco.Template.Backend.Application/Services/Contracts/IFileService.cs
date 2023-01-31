using ExifLibrary;
using Monaco.Template.Backend.Domain.Model;
using SkiaSharp;
using File = Monaco.Template.Backend.Domain.Model.File;

namespace Monaco.Template.Backend.Application.Services.Contracts;

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
	ExifPropertyCollection<ExifProperty> GetMetadata(Stream stream);
	SKImage GetThumbnail(SKImage image, int thumbnailWidth, int thumbnailHeight);
}