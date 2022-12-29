using MockQueryable.Moq;
using Monaco.Template.Application.Infrastructure.Context;
using Monaco.Template.Application.Services;
using Monaco.Template.Common.BlobStorage.Contracts;
using Monaco.Template.Domain.Model;
using Moq;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using File = System.IO.File;

namespace Monaco.Template.Application.Tests.Services;

[ExcludeFromCodeCoverage]
public class FileServiceTests
{
	[Trait("Application Services", "File Service")]
	[Fact(DisplayName = "Upload image succeeds")]
	public async Task UploadImageSucceeds()
	{
		var dbContextMock = new Mock<AppDbContext>();
		var imageDbSetMock = new List<Image>().AsQueryable().BuildMockDbSet();
		dbContextMock.Setup(x => x.Set<Image>())
					 .Returns(imageDbSetMock.Object);
		var blobStorageServiceMock = new Mock<IBlobStorageService>();
		
		var sut = new FileService(dbContextMock.Object, blobStorageServiceMock.Object);

		await using var stream = File.OpenRead("..\\..\\..\\..\\..\\..\\monaco-solid.png");
		await sut.UploadImage(stream, "monaco-solid.png", "image/png", CancellationToken.None);
		stream.Close();

		blobStorageServiceMock.Verify(x => x.UploadTempFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
		dbContextMock.Verify(x => x.Set<Image>().AddAsync(It.IsAny<Image>(), It.IsAny<CancellationToken>()));
		dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()));
	}
}