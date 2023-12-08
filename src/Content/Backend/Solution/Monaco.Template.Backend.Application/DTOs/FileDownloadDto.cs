#if filesSupport
namespace Monaco.Template.Backend.Application.DTOs;

public record FileDownloadDto(Stream FileContent, string FileName, string ContentType);
#endif