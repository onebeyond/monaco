namespace Monaco.Template.Backend.Common.Application.DTOs;

public record FileDownloadDto(Stream FileContent,
							  string FileName,
							  string ContentType);