namespace Monaco.Template.Backend.Domain.Model;

public class Image : File
{
	protected Image()
	{ }

	public Image(Guid id,
				 string name,
				 string extension,
				 long size,
				 string contentType,
				 bool isTemp,
				 int height,
				 int width,
				 DateTime? dateTaken = null,
				 float? gpsLatitude = null,
				 float? gpsLongitude = null,
				 Image? thumbnail = null) : base(id,
												 name,
												 extension,
												 size,
												 contentType,
												 isTemp)
	{
		Dimensions = new(height, width);
		DateTaken = dateTaken;
		Position = gpsLatitude.HasValue && gpsLongitude.HasValue
					   ? new(gpsLatitude.Value, gpsLongitude.Value)
					   : null;
		Thumbnail = thumbnail;
	}

	public DateTime? DateTaken { get; }
	public ImageDimensions Dimensions { get; }
	public GpsPosition? Position { get; }

	public Guid? ThumbnailId { get; protected set; }
	public virtual Image? Thumbnail { get; }

	public override void MakePermanent()
	{
		base.MakePermanent();
		Thumbnail?.MakePermanent();
	}
}