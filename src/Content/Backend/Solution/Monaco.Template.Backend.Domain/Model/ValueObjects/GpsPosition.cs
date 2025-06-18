using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model.ValueObjects;

public class GpsPosition : ValueObject
{
	public const int LatitudeMin = -90;
	public const int LatitudeMax = 90;
	public const int LongitudeMin = -180;
	public const int LongitudeMax = 180;

	protected GpsPosition()
	{ }

	public GpsPosition(float latitude, float longitude)
	{
		Latitude = latitude.Throw()
						   .IfOutOfRange(LatitudeMin, LatitudeMax);
		Longitude = longitude.Throw()
							 .IfOutOfRange(LongitudeMin, LongitudeMax);
	}

	public float Latitude { get; }
	public float Longitude { get; }

	protected override IEnumerable<object?> GetEqualityComponents()
	{
		yield return Latitude;
		yield return Longitude;
	}
}