namespace Monaco.Template.Backend.Application.Features.File.DTOs;

public record FileDto(Guid Id,
					  string Name,
					  string Extension,
					  string ContentType,
					  long Size,
					  DateTime UploadedOn,
					  bool IsTemp);