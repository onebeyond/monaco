namespace Monaco.Template.Application.DTOs;

public class FileDownloadDto
{
    public Stream FileContent { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
}