#if filesSupport
namespace Monaco.Template.Backend.Application.DTOs;

public record FileDto(Guid Id,
					  string Name,
					  string Extension,
					  string ContentType,
					  long Size,
					  DateTime UploadedOn,
					  bool IsTemp);
#endif