namespace Monaco.Template.Backend.Application.DTOs;

public class FileDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string Extension { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long Size { get; set; }
	public DateTime UploadedOn { get; set; }
	public bool IsTemp { get; set; }
}