using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Monaco.Template.Common.Tests.Factories;
using Monaco.Template.Domain.Model;
using Xunit;

namespace Monaco.Template.Domain.Tests;

[ExcludeFromCodeCoverage]
public class CompanyTests
{
	[Trait("Core Domain Entities", "Company Entity")]
	[Theory(DisplayName = "New company succeeds")]
	[AnonymousData]
	public void NewCompanySucceeds(string name,
								   string email,
								   string webSiteUrl,
								   string address,
								   string city,
								   string county,
								   string postCode,
								   Country country)
	{
		var sut = new Company(name,
							  email,
							  webSiteUrl,
							  address,
							  city,
							  county,
							  postCode,
							  country);

		sut.Name.Should().Be(name);
		sut.Email.Should().Be(email);
		sut.WebSiteUrl.Should().Be(webSiteUrl);
		sut.Address.Should().Be(address);
		sut.City.Should().Be(city);
		sut.County.Should().Be(county);
		sut.PostCode.Should().Be(postCode);
		sut.Country.Should().Be(country);
	}

	[Trait("Core Domain Entities", "Company Entity")]
	[Theory(DisplayName = "New company succeeds")]
	[AnonymousData]
	public void UpdateCompanySucceeds(Company sut,
									  string name,
									  string email,
									  string webSiteUrl,
									  string address,
									  string city,
									  string county,
									  string postCode,
									  Country country)
	{
		sut.Update(name,
				   email,
				   webSiteUrl,
				   address,
				   city,
				   county,
				   postCode,
				   country);

		sut.Name.Should().Be(name);
		sut.Email.Should().Be(email);
		sut.WebSiteUrl.Should().Be(webSiteUrl);
		sut.Address.Should().Be(address);
		sut.City.Should().Be(city);
		sut.County.Should().Be(county);
		sut.PostCode.Should().Be(postCode);
		sut.Country.Should().Be(country);
	}
}