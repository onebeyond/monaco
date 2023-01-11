using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
public class AddressTests
{
	[Trait("Core Domain Entities", "Address Entity")]
	[Theory(DisplayName = "New address succeeds")]
	[AnonymousData]
	public void NewAddressSucceeds(string? street,
								   string? city,
								   string? county,
								   string? postCode,
								   Country country)
	{
		postCode = postCode?[..10];
		var sut = new Address(street,
							  city,
							  county,
							  postCode,
							  country);

		sut.Street.Should().Be(street);
		sut.City.Should().Be(city);
		sut.County.Should().Be(county);
		sut.PostCode.Should().Be(postCode);
		sut.Country.Should().Be(country);
	}
}