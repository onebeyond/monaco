using AutoFixture;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Tests;
using Moq;

namespace Monaco.Template.Backend.Common.Domain.Tests.Factories.Entities;

public static class EntityFactory
{
	public static Entity CreateMock(Guid? id = null) =>
		FixtureFactory.Create(f => f.RegisterEntityMock(id))
					  .Create<Entity>();
}

public static class EntityFactoryExtension
{
	public static IFixture RegisterEntityMock(this IFixture fixture, Guid? id = null)
	{
		fixture.Register(() =>
						 {
							 var mock = new Mock<Entity>(id ?? fixture.Create<Guid>())
										{
											CallBase = true
										};
							 return mock.Object;
						 });
		return fixture;
	}
}