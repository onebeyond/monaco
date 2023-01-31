using Monaco.Template.Backend.Application.Features.File.Commands;
using Monaco.Template.Backend.Common.BlobStorage;
using Monaco.Template.Backend.Domain.Model;
using File = Monaco.Template.Backend.Domain.Model.File;

namespace Monaco.Template.Backend.Application.DTOs.Extensions;

public static class FileExtensions
{
	public static FileDto? Map(this File? value)
	{
		if (value == null)
			return null;

		return new FileDto
		{
			Id = value.Id,
			Name = value.Name,
			Extension = value.Extension,
			ContentType = value.ContentType,
			Size = value.Size,
			UploadedOn = value.UploadedOn,
			IsTemp = value.IsTemp
		};
	}

	public static ImageDto? Map(this Image? value)
	{
		if (value == null)
			return null;

		return new ImageDto
		{
			DateTaken = value.DateTaken,
			Width = value.Width,
			Height = value.Height,
			ThumbnailId = value.ThumbnailId,
			Thumbnail = value.ThumbnailId.HasValue ? value.Thumbnail.Map() : null,
			Id = value.Id,
			Name = value.Name,
			Extension = value.Extension,
			ContentType = value.ContentType,
			Size = value.Size,
			UploadedOn = value.UploadedOn,
			IsTemp = value.IsTemp
		};
	}

	public static File Map(this FileCreateCommand value, Guid id, FileTypeEnum fileType) =>
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