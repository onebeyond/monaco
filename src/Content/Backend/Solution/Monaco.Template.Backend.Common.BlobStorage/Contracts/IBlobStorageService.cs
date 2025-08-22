namespace Monaco.Template.Backend.Common.BlobStorage.Contracts;

public interface IBlobStorageService
{
	FileTypeEnum GetFileType(string fileExtension);
	Task<Guid> UploadFileAsync(Stream stream, string fileName, string contentType, string path = "", CancellationToken cancellationToken = default);
	Task<Stream> DownloadAsync(Guid fileName, string path = "", CancellationToken cancellationToken = default);
	Task DeleteAsync(Guid fileName, string path = "", CancellationToken cancellationToken = default);
	Task<Guid> CopyAsync(Guid fileName, string path = "", CancellationToken cancellationToken = default);
}