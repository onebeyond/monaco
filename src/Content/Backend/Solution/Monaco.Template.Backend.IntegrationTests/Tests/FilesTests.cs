using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.IntegrationTests.Apis;
using Refit;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using File = Monaco.Template.Backend.Domain.Model.Entities.File;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Files")]
public class FilesTests : IntegrationTest
{
	public FilesTests(AppFixture fixture) : base(fixture)
	{ }
#if (apiService && auth)

	protected override bool RequiresAuthentication => true;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
#if (auth)

		await SetupAccessToken([Auth.Auth.Roles.Administrator]);
#endif
	}

	[Fact(DisplayName = "Upload File succeeds")]
	public async Task UploadFileSucceeds()
	{
		const string fileExtension = ".png";
		const string fileName = $"CSharp-Logo{fileExtension}";
		const string filePath = $@"Imports\Pictures\{fileName}";
		const string contentType = "image/png";

		var api = GetApi<IFilesApi>(Fixture.WebAppFactory);
		await using var stream = System.IO.File.OpenRead(filePath);
		var response = await api.Upload(new StreamPart(stream, fileName, contentType));
		var uploadDate = DateTime.UtcNow;

		response.StatusCode
				.Should()
				.Be(HttpStatusCode.Created);

		var files = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
								 .Set<File>()
								 .AsNoTracking()
								 .ToListAsync();

		files.Should()
			 .AllBeAssignableTo<Image>()
			 .And
			 .HaveCount(2);
		files.OfType<Image>()
			 .Count(x => x.ThumbnailId.HasValue)
			 .Should()
			 .Be(1);
		files.OfType<Image>()
			 .Count(x => !x.ThumbnailId.HasValue)
			 .Should()
			 .Be(1);
		files.Should()
			 .AllSatisfy(f =>
						 {
							 f.IsTemp
							  .Should()
							  .BeTrue();

							 f.ContentType
							  .Should()
							  .Be(contentType);

							 f.Extension
							  .Should()
							  .Be(fileExtension);

							 f.Size
							  .Should()
							  .BeGreaterThan(0);

							 f.UploadedOn
							  .Should()
							  .BeCloseTo(uploadDate, TimeSpan.FromSeconds(5));
						 });
	}
}