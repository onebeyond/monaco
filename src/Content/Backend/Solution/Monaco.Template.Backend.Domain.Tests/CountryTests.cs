using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Monaco.Template.Common.Tests.Factories;
using Monaco.Template.Domain.Model;
using Xunit;

namespace Monaco.Template.Domain.Tests;

[ExcludeFromCodeCoverage]
public class CountryTests
{
    [Trait("Core Domain Entities", "Country Entity")]
    [Theory(DisplayName = "New country succeeds")]
    [AnonymousData]
    public void NewCountrySucceeds(string name)
    {
        var sut = new Country(name);
			
        sut.Name.Should().Be(name);
    }
}