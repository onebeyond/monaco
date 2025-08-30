using AwesomeAssertions;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Domain.Tests.Factories;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Domain Entities", "Domain Event Entity")]
public class DomainEventTests
{
	[Theory(DisplayName = "New domain event succeeds")]
	[AutoDomainData]
	public void NewDomainEventWithoutParametersSucceeds(DomainEvent sut)
	{
		sut.DateOccurred
		   .Should()
		   .BeCloseTo(DateTime.UtcNow,
					  new TimeSpan(0, 0, 5));
	}
}