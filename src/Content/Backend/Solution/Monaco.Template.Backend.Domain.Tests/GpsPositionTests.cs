using FluentAssertions;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Monaco.Template.Backend.Domain.Model.ValueObjects;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "GpsPosition Entity")]
public class GpsPositionTests
{
	[Fact(DisplayName = "New GpsPosition with valid values succeeds")]
	public void NewGpsPositionWithValidValuesSucceeds()
	{
		var latitude = RandomNumberGenerator.GetInt32(GpsPosition.LatitudeMin, GpsPosition.LatitudeMax);
		var longitude = RandomNumberGenerator.GetInt32(GpsPosition.LongitudeMin, GpsPosition.LongitudeMax);

		var sut = new GpsPosition(latitude, longitude);

		sut.Latitude
		   .Should()
		   .Be(latitude);
		sut.Longitude
		   .Should()
		   .Be(longitude);
	}

	[Theory(DisplayName = "New GpsPosition with invalid positions fails")]
	[InlineData(GpsPosition.LatitudeMin - 1, 0)]
	[InlineData(GpsPosition.LatitudeMax + 1, 0)]
	[InlineData(0, GpsPosition.LongitudeMin - 1)]
	[InlineData(0, GpsPosition.LongitudeMax + 1)]
	public void NewGpsPositionWithInvalidPositionsFails(int latitude, int longitude)
	{
		var sut = () => new GpsPosition(latitude, longitude);

		sut.Should()
		   .ThrowExactly<ArgumentOutOfRangeException>();
	}
}