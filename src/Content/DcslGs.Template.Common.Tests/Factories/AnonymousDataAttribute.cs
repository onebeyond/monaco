using AutoFixture;
using AutoFixture.Xunit2;

namespace DcslGs.Template.Common.Tests.Factories;

public class AnonymousDataAttribute : AutoDataAttribute
{
    private static readonly Func<IFixture> fixture = () => new Fixture().RegisterFactories();

    public AnonymousDataAttribute() : base(fixture)
    {
    }
}