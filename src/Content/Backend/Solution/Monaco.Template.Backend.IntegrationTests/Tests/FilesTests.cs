using FluentAssertions;
using Flurl.Http;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Domain.Model.Entities;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using File = Monaco.Template.Backend.Domain.Model.Entities.File;


namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Trait("Integration Tests", "Files")]
public class FilesTests : IntegrationTest
{
	public FilesTests(AppFixture fixture) : base(fixture)
	{ }

#if (auth)
	protected override bool RequiresAuthentication => true;
#else
	protected override bool RequiresAuthentication => false;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
#if (auth)

		await SetupAccessToken([Auth.Auth.Roles.Administrator]);
#endif
	}

	[Fact(DisplayName = "Upload File succeeds")]
	public async Task UploadFileSuccceeds()
	{
		const string fileExtension = ".png";
		const string fileName = $"CSharp-Logo{fileExtension}";
		const string file = $@"Imports\Pictures\{fileName}";
		const string contentType = "image/png";

		var response = await CreateRequest(ApiRoutes.Files.Post()).PostMultipartAsync(b => b.AddFile("file",
																									 System.IO.File.OpenRead(file),
																									 fileName, contentType));
		var uploadDate = DateTime.UtcNow;

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.Created);

		var files = await GetDbContext().Set<File>()
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