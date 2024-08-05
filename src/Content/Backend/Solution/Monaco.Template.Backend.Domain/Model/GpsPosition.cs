using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model;

public class GpsPosition : ValueObject
{
	protected GpsPosition()
	{ }

	public GpsPosition(float latitude, float longitude)
	{
		Latitude = latitude.Throw()
						   .IfOutOfRange(-90, 90);
		Longitude = longitude.Throw()
							 .IfOutOfRange(-180, 180);
	}

	public float Latitude { get; }
	public float Longitude { get; }

	protected override IEnumerable<object?> GetEqualityComponents()
	{
		yield return Latitude;
		yield return Longitude;
	}
}