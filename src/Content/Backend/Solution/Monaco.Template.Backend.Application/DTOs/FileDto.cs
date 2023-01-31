namespace Monaco.Template.Backend.Application.DTOs;

public class FileDto
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Extension { get; set; }
	public string ContentType { get; set; }
	public long Size { get; set; }
	public DateTime UploadedOn { get; set; }
	public bool IsTemp { get; set; }
}