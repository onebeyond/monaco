using Monaco.Template.Backend.Domain.Model.ValueObjects;

namespace Monaco.Template.Backend.Domain.Model.Entities;

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
				 (int height, int width) dimensions,
				 DateTime? dateTaken = null,
				 (float latitude, float longitude)? gpsPosition = null,
				 (Guid id, long size, (int height, int width) dimensions)? thumbnail = null)
		: base(id,
			   name,
			   extension,
			   size,
			   contentType,
			   isTemp)
	{
		Dimensions = new(dimensions.height, dimensions.width);
		DateTaken = dateTaken;

		if (gpsPosition.HasValue)
			Position = new(gpsPosition.Value.latitude,
						   gpsPosition.Value.longitude);

		if (thumbnail.HasValue)
			Thumbnail = new(thumbnail.Value.id,
							name,
							extension,
							thumbnail.Value.size,
							contentType,
							isTemp,
							thumbnail.Value.dimensions,
							dateTaken,
							gpsPosition);
	}

	public DateTime? DateTaken { get; }
	public ImageDimensions Dimensions { get; } = null!;
	public GpsPosition? Position { get; }

	public Guid? ThumbnailId { get; protected set; }
	public virtual Image? Thumbnail { get; }

	public override void MakePermanent()
	{
		base.MakePermanent();
		Thumbnail?.MakePermanent();
	}

	public override void MarkForRemoval()
	{
		base.MarkForRemoval();
		Thumbnail?.MarkForRemoval();
	}
}