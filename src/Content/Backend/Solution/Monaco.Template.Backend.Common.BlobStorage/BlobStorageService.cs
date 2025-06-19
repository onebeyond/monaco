using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;
using System.Web;

namespace Monaco.Template.Backend.Common.BlobStorage;

public class BlobStorageService : IBlobStorageService
{
	private readonly BlobContainerClient _containerClient;

	public BlobStorageService(BlobServiceClient serviceClient, string containerName)
	{
		_containerClient = serviceClient.GetBlobContainerClient(containerName);
	}

	public async Task<Guid> UploadFileAsync(Stream stream, string fileName, string contentType, string path = "", CancellationToken cancellationToken = default)
	{
		var id = Guid.NewGuid();
		var blobName = GetBlobName(id.ToString(), path);
		var metadata = GetMetadata(fileName, contentType);
		var blobClient = _containerClient.GetBlockBlobClient(blobName);
		await blobClient.UploadAsync(stream, null, metadata, cancellationToken: cancellationToken);
		return id;
	}

	//public async Task<Guid> UploadTempFileAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
	//{
	//	var id = Guid.NewGuid();
	//	var tempBlobName = GetTempPath(id.ToString());
	//	var metadata = GetMetadata(fileName, contentType);
	//	var blobClient = _containerClient.GetBlockBlobClient(tempBlobName);
	//	await blobClient.UploadAsync(stream, null, metadata, cancellationToken: cancellationToken);
	//	return id;
	//}
	
	public async Task<Stream> DownloadAsync(Guid fileName, string path = "", CancellationToken cancellationToken = default)
	{
		var blobName = GetBlobName(fileName.ToString(), path);
		var client = _containerClient.GetBlockBlobClient(blobName);
		var stream = await client.OpenReadAsync(false, cancellationToken: cancellationToken);
		return stream;
	}

	//public async Task<Stream> DownloadAsync(Guid fileName, bool isTemp, CancellationToken cancellationToken)
	//{
	//	var blobName = GetBlobName(fileName.ToString(), isTemp);
	//	var client = _containerClient.GetBlockBlobClient(blobName);
	//	var stream = await client.OpenReadAsync(false, cancellationToken: cancellationToken);
	//	return stream;
	//}

	//public async Task MakePermanentAsync(Guid fileName, CancellationToken cancellationToken)
	//{
	//	var blobName = GetTempPath(fileName.ToString());
	//	var sourceClient = _containerClient.GetBlobClient(blobName);
	//	var destClient = _containerClient.GetBlobClient(fileName.ToString());
	//	var copyOperation = await destClient.StartCopyFromUriAsync(sourceClient.Uri, cancellationToken: cancellationToken);
	//	await copyOperation.WaitForCompletionAsync(cancellationToken);
	//	await sourceClient.DeleteAsync(cancellationToken: cancellationToken);
	//}

	public async Task DeleteAsync(Guid fileName, string path = "", CancellationToken cancellationToken = default)
	{
		var blobName = GetBlobName(fileName.ToString(), path);
		var client = _containerClient.GetBlockBlobClient(blobName);
		await client.DeleteIfExistsAsync(cancellationToken: cancellationToken);
	}

	//public async Task DeleteAsync(Guid fileName, bool isTemp, CancellationToken cancellationToken)
	//{
	//	var blobName = GetBlobName(fileName.ToString(), isTemp);
	//	var client = _containerClient.GetBlockBlobClient(blobName);
	//	await client.DeleteIfExistsAsync(cancellationToken: cancellationToken);
	//}

	public async Task<Guid> CopyAsync(Guid fileName, string path = "", CancellationToken cancellationToken = default)
	{
		var sourceBlobName = GetBlobName(fileName.ToString(), path);
		var sourceClient = _containerClient.GetBlobClient(sourceBlobName);
		var destId = Guid.NewGuid();
		var destBlobName = GetBlobName(destId.ToString(), path);
		var destClient = _containerClient.GetBlobClient(destBlobName);
		var copyOperation = await destClient.StartCopyFromUriAsync(sourceClient.Uri, cancellationToken: cancellationToken);
		await copyOperation.WaitForCompletionAsync(cancellationToken);
		return destId;
	}

	//public async Task<Guid> CopyAsync(Guid fileName, bool isTemp, CancellationToken cancellationToken)
	//{
	//	var sourceBlobName = GetBlobName(fileName.ToString(), isTemp);
	//	var sourceClient = _containerClient.GetBlobClient(sourceBlobName);
	//	var destId = Guid.NewGuid();
	//	var destBlobName = GetBlobName(destId.ToString(), isTemp);
	//	var destClient = _containerClient.GetBlobClient(destBlobName);
	//	var copyOperation = await destClient.StartCopyFromUriAsync(sourceClient.Uri, cancellationToken: cancellationToken);
	//	await copyOperation.WaitForCompletionAsync(cancellationToken);
	//	return destId;
	//}

	public FileTypeEnum GetFileType(string fileExtension) =>
		fileExtension.Trim('.').ToLower() switch
		{
			"doc" or "docx" or "pdf" or "rtf" or "txt" or "xls" or "xlsx" or "xlsm" => FileTypeEnum.Document,
			"jpg" or "jpeg" or "png" or "bmp" or "gif" or "tif" or "tiff" => FileTypeEnum.Image,
			_ => FileTypeEnum.Others,
		};

	//private static string GetTempPath(string blobFileName) => $"temp/{blobFileName}";
	
	private static string GetBlobName(string blobFileName, string path = "") => string.IsNullOrWhiteSpace(path) ? blobFileName : $"{path}/{blobFileName}";

	//private static string GetBlobName(string blobFileName, bool isTemp) => $"{(isTemp ? "temp/" : string.Empty)}{blobFileName}";

	private Dictionary<string, string> GetMetadata(string fileName, string contentType) =>
		new()
		{
			[BlobMetadata.Name] = HttpUtility.UrlEncode(Path.GetFileNameWithoutExtension(fileName)),
			[BlobMetadata.Extension] = HttpUtility.UrlEncode(Path.GetExtension(fileName)),
			[BlobMetadata.ContentType] = contentType,
			[BlobMetadata.UploadedOn] = DateTime.UtcNow.ToString("O"),
			[BlobMetadata.FileType] = GetFileType(Path.GetExtension(fileName)).ToString()
		};
}