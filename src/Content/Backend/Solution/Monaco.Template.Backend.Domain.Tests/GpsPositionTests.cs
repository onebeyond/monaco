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
	[Fact(DisplayName = "New GpsPosition succeeds")]
	public void NewGpsPositionSucceeds()
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
}