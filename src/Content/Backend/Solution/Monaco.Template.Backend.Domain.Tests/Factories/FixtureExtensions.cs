using AutoFixture;
using Monaco.Template.Backend.Domain.Tests.Factories.Entities;

namespace Monaco.Template.Backend.Domain.Tests.Factories;

public static class FixtureExtensions
{
	public static IFixture RegisterEntityFactories(this IFixture fixture) =>
		fixture.RegisterCompany()
			   .RegisterAddress()
#if (!filesSupport)
			   .RegisterCountry();
#else
			   .RegisterCountry()
			   .RegisterDocument()
			   .RegisterImage()
			   .RegisterProduct();
#endif

	public static void RegisterMockFactories(this IFixture fixture) =>
		fixture.RegisterCompanyMock()
			   .RegisterAddressMock()
#if (!filesSupport)
			   .RegisterCountryMock();
#else
			   .RegisterCountryMock()
			   .RegisterDocument()
			   .RegisterImage()
			   .RegisterProductMock();
#endif
}