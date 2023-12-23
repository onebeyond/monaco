using Dawn;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Domain.Model;

public class GpsPosition : ValueObject
{
	protected GpsPosition()
	{ }

	public GpsPosition(float latitude, float longitude)
	{
		Latitude = Guard.Argument(latitude, nameof(latitude))
						.InRange(-90, 90);
		Longitude = Guard.Argument(longitude, nameof(longitude))
						 .InRange(-180, 180);
	}

	public float Latitude { get; }
	public float Longitude { get; }

	protected override IEnumerable<object?> GetEqualityComponents()
	{
		yield return Latitude;
		yield return Longitude;
	}
}