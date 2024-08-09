using AutoFixture;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Tests;
using Moq;

namespace Monaco.Template.Backend.Common.Domain.Tests.Factories.Entities;

public static class EnumerationFactory
{
	public static Enumeration CreateMock((Guid Id, string Name)? value = null) =>
		FixtureFactory.Create(f => f.RegisterEnumerationMock(value))
					  .Create<Enumeration>();
}

public static class EnumerationFactoryExtension
{
	public static IFixture RegisterEnumerationMock(this IFixture fixture,
												   (Guid Id, string Name)? value = null)
	{
		fixture.Register(() =>
						 {
							 var mock = new Mock<Enumeration>(value.HasValue
																  ?
																  [
																	  value.Value.Id,
																	  value.Value.Name
																  ]
																  :
																  [
																	  fixture.Create<Guid>(),
																	  fixture.Create<string>()
																  ])
										{
											CallBase = true
										};
							 return mock.Object;
						 });
		return fixture;
	}
}