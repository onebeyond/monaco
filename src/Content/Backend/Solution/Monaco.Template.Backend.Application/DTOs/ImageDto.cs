namespace Monaco.Template.Backend.Application.DTOs;

public class ImageDto : FileDto
{
	public DateTime? DateTaken { get; set; }
	public int Width { get; set; }
	public int Height { get; set; }
	public Guid? ThumbnailId { get; set; }
	public ImageDto? Thumbnail { get; set; }
}