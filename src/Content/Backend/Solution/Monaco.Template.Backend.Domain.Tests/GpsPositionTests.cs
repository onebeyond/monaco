using FluentAssertions;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "GpsPosition Entity")]
public class GpsPositionTests
{
	[Fact(DisplayName = "New GpsPosition with valid values succeeds")]
	public void NewGpsPositionWithValidValuesSucceeds()
	{
		var latitude = RandomNumberGenerator.GetInt32(-90, 90);
		var longitude = RandomNumberGenerator.GetInt32(-180, 180);

		var sut = new GpsPosition(latitude, longitude);

		sut.Latitude
		   .Should()
		   .Be(latitude);
		sut.Longitude
		   .Should()
		   .Be(longitude);
	}

	[InlineData(-91, 0)]
	[InlineData(91, 0)]
	[InlineData(0, -181)]
	[InlineData(0, 181)]
	[Theory(DisplayName = "New GpsPosition with invalid positions fails")]
	public void NewGpsPositionWithInvalidPositionsFails(int latitude, int longitude)
	{
		var sut = () => new GpsPosition(latitude, longitude);
		sut.Should()
		   .Throw<ArgumentOutOfRangeException>();
	}
}