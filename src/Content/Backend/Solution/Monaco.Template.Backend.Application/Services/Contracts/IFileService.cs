﻿using ExifLibrary;
using Monaco.Template.Backend.Common.Application.DTOs;
using Monaco.Template.Backend.Domain.Model.Entities;
using SkiaSharp;
using File = Monaco.Template.Backend.Domain.Model.Entities.File;

namespace Monaco.Template.Backend.Application.Services.Contracts;

public interface IFileService
{
	Task<File> UploadAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
	Task<Document> UploadDocumentAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
	Task<Image> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken);
	Task<FileDownloadDto> DownloadFileAsync(File item, CancellationToken cancellationToken);
	Task DeleteFileAsync(File file, CancellationToken cancellationToken);
	Task DeleteFilesAsync(File[] files, CancellationToken cancellationToken);
	Task DeleteDocumentAsync(Document file, CancellationToken cancellationToken);
	Task DeleteImageAsync(Image image, CancellationToken cancellationToken);
	Task DeleteDocumentsAsync(Document[] documents, CancellationToken cancellationToken);
	Task DeleteImagesAsync(Image[] images, CancellationToken cancellationToken);
	Task<File> CopyFileAsync(File file, CancellationToken cancellationToken);
	ExifPropertyCollection<ExifProperty> GetMetadata(Stream stream);
	SKImage GetThumbnail(SKImage image, int thumbnailWidth, int thumbnailHeight);
}