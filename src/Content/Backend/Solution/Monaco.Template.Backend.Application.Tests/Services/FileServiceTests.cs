﻿using MockQueryable.Moq;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services;
using Monaco.Template.Backend.Common.BlobStorage.Contracts;
using Monaco.Template.Backend.Domain.Model;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Services;

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

		const string imgBase64 = "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";

		await using var stream = new MemoryStream(Convert.FromBase64String(imgBase64));
		await sut.UploadImage(stream, "sample-image.png", "image/png", CancellationToken.None);
		stream.Close();

		blobStorageServiceMock.Verify(x => x.UploadTempFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
		dbContextMock.Verify(x => x.Set<Image>().AddAsync(It.IsAny<Image>(), It.IsAny<CancellationToken>()));
		dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()));
	}
}