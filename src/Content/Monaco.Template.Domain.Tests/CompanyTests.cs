﻿using System.Diagnostics.CodeAnalysis;
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
	}

	[Trait("Core Domain Entities", "Company Entity")]
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
	}
}