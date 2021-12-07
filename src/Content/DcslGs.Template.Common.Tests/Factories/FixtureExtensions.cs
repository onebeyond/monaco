using AutoFixture;
using AutoFixture.AutoMoq;
using DcslGs.Template.Common.Tests.Factories.Entities;

namespace DcslGs.Template.Common.Tests.Factories;

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

        fixture.RegisterCompany()
               .RegisterCountry();

        return fixture;
    }
}