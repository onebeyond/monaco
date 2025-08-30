using AwesomeAssertions;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.Domain.Model.ValueObjects;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Address Entity")]
public class AddressTests
{
	[Theory(DisplayName = "New address succeeds")]
	[AutoDomainData]
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

	[Theory(DisplayName = "New address with only country succeeds")]
	[AutoDomainData]
	public void NewAddressWithOnlyCountrySucceeds(Country country)
	{
		var sut = new Address(null,
							  null,
							  null,
							  null,
							  country);

		sut.Country.Should().Be(country);
	}

	[Theory(DisplayName = "New address with Street too long throws")]
	[AutoDomainData]
	public void NewAddressWithStreetTooLongThrows(Country country)
	{
		var street = new string(It.IsAny<char>(), Address.StreetLength + 1);

		var sut = () => new Address(street,
									null,
									null,
									null,
									country);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New address with City too long throws")]
	[AutoDomainData]
	public void NewAddressWithCityTooLongThrows(Country country)
	{
		var city = new string(It.IsAny<char>(), Address.CityLength + 1);

		var sut = () => new Address(null,
									city,
									null,
									null,
									country);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New address with County too long throws")]
	[AutoDomainData]
	public void NewAddressWithCountyTooLongThrows(Country country)
	{
		var county = new string(It.IsAny<char>(), Address.CountyLength + 1);

		var sut = () => new Address(null,
									null,
									county,
									null,
									country);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New address with PostCode too long throws")]
	[AutoDomainData]
	public void NewAddressWithPostCodeTooLongThrows(Country country)
	{
		var postCode = new string(It.IsAny<char>(), Address.PostCodeLength + 1);

		var sut = () => new Address(null,
									null,
									null,
									postCode,
									country);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Fact(DisplayName = "New address with no country throws")]
	public void NewAddressWithNoCountryThrows()
	{
		var sut = () => new Address(null,
									null,
									null,
									null,
									null!);

		sut.Should()
		   .ThrowExactly<ArgumentNullException>();
	}
}