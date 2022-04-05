using AutoFixture;
using AutoFixture.Xunit2;

namespace DcslGs.Template.Common.Tests.Factories;

public class AnonymousDataAttribute : AutoDataAttribute
{
	public AnonymousDataAttribute(bool mockedData = false) : base(() => mockedData
																			? new Fixture().RegisterMockFactories()
																			: new Fixture().RegisterFactories())
	{
	}
}