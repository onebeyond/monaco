using AutoFixture;
using Monaco.Template.Backend.Domain.Model;
using Moq;

namespace Monaco.Template.Backend.Common.Tests.Factories.Entities;

public class ImageFactory
{
	public static Image Create() =>
		new Fixture().RegisterImage()
					 .Create<Image>();

	public static IEnumerable<Image> CreateMany() =>
		new Fixture().RegisterImage()
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
											  fixture.Create<int>(),
											  fixture.Create<int>(),
											  fixture.Create<DateTime>(),
											  null,
											  null,
											  new Image(fixture.Create<Guid>(),
														name,
														extension,
														size,
														contentType,
														false,
														fixture.Create<int>(),
														fixture.Create<int>()));
						 });
		return fixture;
	}

	public static IFixture RegisterImageMock(this IFixture fixture)
	{
		fixture.Register(() =>
						 {
							 var name = fixture.Create<string>();
							 const string extension = ".png";
							 var size = fixture.Create<long>();
							 const string contentType = "image/png";

							 var mock = new Mock<Image>(fixture.Create<Guid>(),
														name,
														extension,
														size,
														contentType,
														false,
														fixture.Create<int>(),
														fixture.Create<int>(),
														fixture.Create<DateTime>(),
														null,
														null,
														new Image(fixture.Create<Guid>(),
																  name,
																  extension,
																  size,
																  contentType,
																  false,
																  fixture.Create<int>(),
																  fixture.Create<int>()));
							 return mock.Object;
						 });
		return fixture;
	}
}