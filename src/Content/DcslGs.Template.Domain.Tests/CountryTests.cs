using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using DcslGs.Template.Common.Tests.Factories;
using DcslGs.Template.Domain.Model;
using Xunit;

namespace DcslGs.Template.Domain.Tests;

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