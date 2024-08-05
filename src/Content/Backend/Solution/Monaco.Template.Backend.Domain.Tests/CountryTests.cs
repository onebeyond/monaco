using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Country Entity")]
public class CountryTests
{
	[Theory(DisplayName = "New country succeeds")]
	[AnonymousData]
	public void NewCountrySucceeds(string name)
	{
		var sut = new Country(name);

		sut.Name.Should().Be(name);
	}

	[Fact(DisplayName = "New country with empty name fails")]
	public void NewCountryWithEmptyNameFails()
	{
		var sut = () => new Country(string.Empty);

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}

	[Fact(DisplayName = "New country with name too long fails")]
	public void NewCountryWithNameTooLongFails()
	{
		var sut = () => new Country(new string(It.IsAny<char>(), 101));

		sut.Should()
		   .ThrowExactly<ArgumentException>();
	}
}