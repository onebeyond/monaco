using Monaco.Template.Backend.Common.BlobStorage;

namespace Monaco.Template.Backend.Common.BlobStorage.Contracts;

public interface IBlobStorageService
{
	//Task<Stream> DownloadAsync(Guid fileName, bool isTemp, CancellationToken cancellationToken);
	//Task<Guid> UploadTempFileAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
	//Task MakePermanentAsync(Guid fileName, CancellationToken cancellationToken);
	//Task DeleteAsync(Guid fileName, bool isTemp, CancellationToken cancellationToken);
	//Task<Guid> CopyAsync(Guid fileName, bool isTemp, CancellationToken cancellationToken);
	FileTypeEnum GetFileType(string fileExtension);
	Task<Guid> UploadFileAsync(Stream stream, string fileName, string contentType, string path = "", CancellationToken cancellationToken = default);
	Task<Stream> DownloadAsync(Guid fileName, string path = "", CancellationToken cancellationToken = default);
	Task DeleteAsync(Guid fileName, string path = "", CancellationToken cancellationToken = default);
	Task<Guid> CopyAsync(Guid fileName, string path = "", CancellationToken cancellationToken = default);
}