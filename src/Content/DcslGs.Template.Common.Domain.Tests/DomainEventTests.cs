using FluentAssertions;
using System;
using System.Diagnostics.CodeAnalysis;
using DcslGs.Template.Common.Domain.Model;
using DcslGs.Template.Common.Domain.Tests.AutoFixture;
using Xunit;

namespace DcslGs.Template.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
public class DomainEventTests
{
    [Trait("Common domain Entities", "Domain Event Entity")]
    [Theory(DisplayName = "New domain event succeeds")]
    [AnonymousData]
    public void NewEntityWithoutParametersSucceeds(DomainEvent sut)
    {
        sut.DateOccurred.Should().BeCloseTo(DateTime.UtcNow, new TimeSpan(0,0,5));
    }
}