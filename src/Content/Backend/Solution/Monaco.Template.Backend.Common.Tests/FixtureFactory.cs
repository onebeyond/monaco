using AutoFixture;
using AutoFixture.AutoMoq;

namespace Monaco.Template.Backend.Common.Tests;

public static class FixtureFactory
{
	public static IFixture Create(Action<IFixture> configureFactories)
	{
		var fixture = new Fixture();
		fixture.Behaviors
			   .OfType<ThrowingRecursionBehavior>()
			   .ToList()
			   .ForEach(b => fixture.Behaviors.Remove(b));

		fixture.Behaviors
			   .Add(new OmitOnRecursionBehavior(1));

		fixture.Customize(new AutoMoqCustomization());

		configureFactories.Invoke(fixture);

		return fixture;
	}
}