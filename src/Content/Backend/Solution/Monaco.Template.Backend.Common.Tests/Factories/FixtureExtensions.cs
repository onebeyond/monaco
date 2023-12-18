using AutoFixture;
using AutoFixture.AutoMoq;
using Monaco.Template.Backend.Common.Tests.Factories.Entities;

namespace Monaco.Template.Backend.Common.Tests.Factories;

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
			   .RegisterAddress()
			   .RegisterCountry()
			   .RegisterDocument()
			   .RegisterImage()
			   .RegisterProduct();

		return fixture;
	}

	public static IFixture RegisterMockFactories(this IFixture fixture)
	{
		fixture.Behaviors
			   .OfType<ThrowingRecursionBehavior>()
			   .ToList()
			   .ForEach(b => fixture.Behaviors.Remove(b));

		fixture.Behaviors
			   .Add(new OmitOnRecursionBehavior(1));

		fixture.Customize(new AutoMoqCustomization());

		fixture.RegisterCompanyMock()
			   .RegisterAddressMock()
			   .RegisterCountryMock()
			   .RegisterDocumentMock()
			   .RegisterImage()
			   .RegisterProductMock();

		return fixture;
	}
}