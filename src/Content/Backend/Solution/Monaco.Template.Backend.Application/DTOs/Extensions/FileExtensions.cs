﻿using Monaco.Template.Backend.Application.Features.File;
using Monaco.Template.Backend.Common.BlobStorage;
using Monaco.Template.Backend.Domain.Model;
using File = Monaco.Template.Backend.Domain.Model.File;

namespace Monaco.Template.Backend.Application.DTOs.Extensions;

public static class FileExtensions
{
	public static FileDto? Map(this File? value) =>
		value is null
			? null
			: new(value.Id,
				  value.Name,
				  value.Extension,
				  value.ContentType,
				  value.Size,
				  value.UploadedOn,
				  value.IsTemp);

	public static ImageDto? Map(this Image? value) =>
		value is null
			? null
			: new(value.Id,
				  value.Name,
				  value.Extension,
				  value.ContentType,
				  value.Size,
				  value.UploadedOn,
				  value.IsTemp,
				  value.DateTaken,
				  value.Dimensions.Width,
				  value.Dimensions.Height,
				  value.ThumbnailId,
				  value.ThumbnailId.HasValue ? value.Thumbnail.Map() : null);

	public static File Map(this CreateFile.Command value, Guid id, FileTypeEnum fileType) =>
		fileType switch
		{
			FileTypeEnum.Image => new Image(id,
											Path.GetFileNameWithoutExtension(value.FileName),
											Path.GetExtension(value.FileName),
											value.Stream.Length,
											value.ContentType,
											true,
											0,
											0),
			_ => new Document(id,
							  Path.GetFileNameWithoutExtension(value.FileName),
							  Path.GetExtension(value.FileName),
							  value.Stream.Length,
							  value.ContentType,
							  true)
		};
}