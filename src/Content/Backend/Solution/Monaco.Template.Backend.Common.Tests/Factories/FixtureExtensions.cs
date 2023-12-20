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
			   #if (excludeFilesSupport)
			   .RegisterCountry();
			   #else
			   .RegisterCountry()
			   .RegisterDocument()
			   .RegisterImage()
			   .RegisterProduct();
			   #endif

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
			   #if (excludeFilesSupport)
			   .RegisterCountryMock();
			   #else
			   .RegisterCountryMock()
			   .RegisterDocumentMock()
			   .RegisterImage()
			   .RegisterProductMock();
			   #endif

		return fixture;
	}
}