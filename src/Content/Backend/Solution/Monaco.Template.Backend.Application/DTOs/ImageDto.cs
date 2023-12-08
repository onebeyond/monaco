﻿#if filesSupport
namespace Monaco.Template.Backend.Application.DTOs;

public record ImageDto(Guid Id,
					   string Name,
					   string Extension,
					   string ContentType,
					   long Size,
					   DateTime UploadedOn,
					   bool IsTemp,
					   DateTime? DateTaken,
					   int Width,
					   int Height,
					   Guid? ThumbnailId,
					   ImageDto? Thumbnail) : FileDto(Id,
													  Name,
													  Extension,
													  ContentType,
													  Size,
													  UploadedOn,
													  IsTemp);
#endif