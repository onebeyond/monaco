using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
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
}