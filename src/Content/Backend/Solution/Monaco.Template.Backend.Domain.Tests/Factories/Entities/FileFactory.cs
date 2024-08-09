using AutoFixture;
using Monaco.Template.Backend.Common.Tests;
using Moq;
using File = Monaco.Template.Backend.Domain.Model.File;

namespace Monaco.Template.Backend.Domain.Tests.Factories.Entities;

public static class FileFactory
{
	public static File CreateMock((Guid Id,
									  string Name,
									  string Extension,
									  long Size,
									  string ContentType,
									  bool IsTemp,
									  DateTime UploadedOn)? value = null) =>
		FixtureFactory.Create(f => f.RegisterFileMock(value))
					  .Create<File>();
}

public static class FileFactoryExtensions
{
	public static IFixture RegisterFileMock(this IFixture fixture,
												(Guid Id,
												string Name,
												string Extension,
												long Size,
												string ContentType,
												bool IsTemp,
												DateTime UploadedOn)? value = null)
	{
		fixture.Register(() =>
						 {
							 try
							 {
								 var mock = new Mock<File>(value.HasValue
															   ?
															   [
																   value.Value.Id,
																   value.Value.Name,
																   value.Value.Extension,
																   value.Value.Size,
																   value.Value.ContentType,
																   value.Value.IsTemp
															   ]
															   :
															   [
																   fixture.Create<Guid>(),
																   fixture.Create<string>(),
																   fixture.Create<string>(),
																   fixture.Create<long>(),
																   fixture.Create<string>(),
																   fixture.Create<bool>()
															   ]) { CallBase = true };
								 mock.SetupGet(x => x.UploadedOn)
									 .Returns(value?.UploadedOn ?? fixture.Create<DateTime>());
								 //mock.Setup(x => x.MakePermanent())
									// .CallBase();
								 return mock.Object;
							 }
							 catch (Exception e)
							 {
								 Console.WriteLine(e);
								 throw;
							 }

						 });
		return fixture;
	}
}