using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using Moq;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Address Entity")]
public class AddressTests
{
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

	[Theory(DisplayName = "New address with only country succeeds")]
	[AnonymousData]
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
	[AnonymousData]
	public void NewAddressWithStreetTooLongThrows(Country country)
	{
		var street = new string(It.IsAny<char>(), 101);

		var sut = () => new Address(street,
									null,
									null,
									null,
									country);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New address with City too long throws")]
	[AnonymousData]
	public void NewAddressWithCityTooLongThrows(Country country)
	{
		var city = new string(It.IsAny<char>(), 101);

		var sut = () => new Address(null,
									city,
									null,
									null,
									country);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New address with County too long throws")]
	[AnonymousData]
	public void NewAddressWithCountyTooLongThrows(Country country)
	{
		var county = new string(It.IsAny<char>(), 101);

		var sut = () => new Address(null,
									null,
									county,
									null,
									country);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Theory(DisplayName = "New address with PostCode too long throws")]
	[AnonymousData]
	public void NewAddressWithPostCodeTooLongThrows(Country country)
	{
		var postCode = new string(It.IsAny<char>(), 11);

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