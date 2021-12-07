using System.Linq;
using AutoFixture;
using AutoFixture.AutoMoq;

namespace DcslGs.Template.Common.Domain.Tests.AutoFixture;

public static class FixtureExtensions
{
    public static IFixture RegisterFactories(this IFixture fixture)
    {
        fixture.Behaviors
               .OfType<ThrowingRecursionBehavior>()
               .ToList()
               .ForEach(b => fixture.Behaviors.Remove(b));

        fixture.Behaviors
               .Add(new OmitOnRecursionBehavior(1));

        fixture.Customize(new AutoMoqCustomization());
			
        return fixture;
    }
}