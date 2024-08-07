using AutoFixture;
using Monaco.Template.Backend.Common.Domain.Tests.Factories.Entities;

namespace Monaco.Template.Backend.Common.Domain.Tests.Factories;

public static class FixtureExtensions
{
	public static IFixture RegisterMockFactories(this IFixture fixture) =>
		fixture.RegisterEntityMock()
			   .RegisterEnumerationMock();
}