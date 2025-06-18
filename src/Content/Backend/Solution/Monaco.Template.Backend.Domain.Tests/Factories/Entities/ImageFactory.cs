using AutoFixture;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Model.Entities;

namespace Monaco.Template.Backend.Domain.Tests.Factories.Entities;

public static class ImageFactory
{
	public static Image Create() =>
		FixtureFactory.Create(f => f.RegisterImage())
					  .Create<Image>();

	public static IEnumerable<Image> CreateMany() =>
		FixtureFactory.Create(f => f.RegisterImage())
					  .CreateMany<Image>();
}

public static class ImageFactoryExtension
{
	public static IFixture RegisterImage(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var name = fixture.Create<string>();
							 const string extension = ".png";
							 var size = fixture.Create<long>();
							 const string contentType = "image/png";

							 return new Image(fixture.Create<Guid>(),
											  name,
											  extension,
											  size,
											  contentType,
											  false,
											  (fixture.Create<int>(),
											   fixture.Create<int>()),
											  fixture.Create<DateTime>(),
											  null,
											  (fixture.Create<Guid>(),
											   size,
											   (fixture.Create<int>(),
												fixture.Create<int>())));
						 });
		return fixture;
	}
}