using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Company Entity")]
public class CompanyTests
{
	[Theory(DisplayName = "New company succeeds")]
	[AnonymousData]
	public void NewCompanySucceeds(string name,
								   string email,
								   string webSiteUrl,
								   Address address)
	{
		var sut = new Company(name,
							  email,
							  webSiteUrl,
							  address);

		sut.Name.Should().Be(name);
		sut.Email.Should().Be(email);
		sut.WebSiteUrl.Should().Be(webSiteUrl);
		sut.Address.Should().Be(address);
		sut.Version.Should().BeNull();
	}

	[Theory(DisplayName = "New company succeeds")]
	[AnonymousData]
	public void UpdateCompanySucceeds(Company sut,
									  string name,
									  string email,
									  string webSiteUrl,
									  Address address)
	{
		sut.Update(name,
				   email,
				   webSiteUrl,
				   address);

		sut.Name.Should().Be(name);
		sut.Email.Should().Be(email);
		sut.WebSiteUrl.Should().Be(webSiteUrl);
		sut.Address.Should().Be(address);
		sut.Version.Should().BeNull();
	}
}