namespace Monaco.Template.Backend.Application.DTOs;

public class FileDownloadDto
{
	public Stream FileContent { get; set; }
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
}