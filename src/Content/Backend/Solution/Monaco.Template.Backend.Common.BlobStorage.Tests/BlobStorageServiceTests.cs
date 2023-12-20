using Azure.Storage.Blobs;
using FluentAssertions;
using Moq;
using Xunit;
using System.Diagnostics.CodeAnalysis;

namespace Monaco.Template.Backend.Common.BlobStorage.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Application Services", "Blob Storage Service")]
public class BlobStorageServiceTests
{
	private readonly BlobStorageService _blobStorageService = new(new Mock<BlobServiceClient>().Object, It.IsAny<string>());

	[Theory(DisplayName = "Get file type succeeds")]
	[MemberData(nameof(GetFileTypeTestData))]
	public void GetFileTypeSucceeds(string fileExtension, FileTypeEnum expected)
	{
		var fileType = _blobStorageService.GetFileType(fileExtension);
		fileType.Should().Be(expected);
	}

	public static IEnumerable<object[]> GetFileTypeTestData =>
		new List<object[]>
		{
			new object[] { ".doc", FileTypeEnum.Document },
			new object[] { ".docx", FileTypeEnum.Document },
			new object[] { ".pdf", FileTypeEnum.Document },
			new object[] { ".rtf", FileTypeEnum.Document },
			new object[] { ".txt", FileTypeEnum.Document },
			new object[] { ".xls", FileTypeEnum.Document },
			new object[] { ".xlsx", FileTypeEnum.Document },
			new object[] { ".xlsm", FileTypeEnum.Document },
			new object[] { ".jpg", FileTypeEnum.Image },
			new object[] { ".jpeg", FileTypeEnum.Image },
			new object[] { ".png", FileTypeEnum.Image },
			new object[] { ".bmp", FileTypeEnum.Image },
			new object[] { ".gif", FileTypeEnum.Image },
			new object[] { ".tif", FileTypeEnum.Image },
			new object[] { ".tiff", FileTypeEnum.Image },
			new object[] { ".other", FileTypeEnum.Others }
		};
}