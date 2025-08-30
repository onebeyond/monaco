using AwesomeAssertions;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Monaco.Template.Backend.Domain.Tests.Factories.Entities;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using File = Monaco.Template.Backend.Domain.Model.Entities.File;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "File Entity")]
public class FileTests
{
	[Theory(DisplayName = "New File succeeds")]
	[AutoDomainData]
	public void NewFileSucceeds(Guid id,
								string name,
								string extension,
								long size,
								string contentType,
								bool isTemp,
								DateTime uploadedOn)
	{
		extension = extension[..File.ExtensionLength];

		var sut = FileFactory.CreateMock((id,
										  name,
										  extension,
										  size,
										  contentType,
										  isTemp,
										  uploadedOn));

		sut.Id.Should().Be(id);
		sut.Name.Should().Be(name);
		sut.Extension.Should().Be(extension);
		sut.Size.Should().Be(size);
		sut.ContentType.Should().Be(contentType);
		sut.IsTemp.Should().Be(isTemp);
		sut.UploadedOn.Should().Be(uploadedOn);
	}

	[Theory(DisplayName = "New File with empty name fails")]
	[AutoDomainData]
	public void NewFileWithEmptyNameFails(Guid id,
										  string extension,
										  long size,
										  string contentType,
										  bool isTemp,
										  DateTime uploadedOn)
	{
		extension = extension[..File.ExtensionLength];

		var sut = () => FileFactory.CreateMock((id,
												string.Empty,
												extension,
												size,
												contentType,
												isTemp,
												uploadedOn));

		sut.Should()
		   .Throw<Exception>()
		   .WithInnerException<Exception>()
		   .WithInnerException<ArgumentException>();
	}

	[Theory(DisplayName = "New File with name too large fails")]
	[AutoDomainData]
	public void NewFileWithNameTooLargeFails(Guid id,
										  string extension,
										  long size,
										  string contentType,
										  bool isTemp,
										  DateTime uploadedOn)
	{
		extension = extension[..File.ExtensionLength];

		var sut = () => FileFactory.CreateMock((id,
												new string(It.IsAny<char>(),
														   File.NameLength + 1),
												extension,
												size,
												contentType,
												isTemp,
												uploadedOn));

		sut.Should()
		   .Throw<Exception>()
		   .WithInnerException<Exception>()
		   .WithInnerException<ArgumentException>();
	}

	[Theory(DisplayName = "New File with empty extension fails")]
	[AutoDomainData]
	public void NewFileWithEmptyExtensionFails(Guid id,
											   string name,
											   long size,
											   string contentType,
											   bool isTemp,
											   DateTime uploadedOn)
	{
		var sut = () => FileFactory.CreateMock((id,
												name,
												string.Empty,
												size,
												contentType,
												isTemp,
												uploadedOn));

		sut.Should()
		   .Throw<Exception>()
		   .WithInnerException<Exception>()
		   .WithInnerException<ArgumentException>();
	}

	[Theory(DisplayName = "New File with extension too large fails")]
	[AutoDomainData]
	public void NewFileWithExtensionTooLargeFails(Guid id,
											 string name,
											 long size,
											 string contentType,
											 bool isTemp,
											 DateTime uploadedOn)
	{
		var sut = () => FileFactory.CreateMock((id,
												name,
												new string(It.IsAny<char>(),
														   File.ExtensionLength + 1),
												size,
												contentType,
												isTemp,
												uploadedOn));

		sut.Should()
		   .Throw<Exception>()
		   .WithInnerException<Exception>()
		   .WithInnerException<ArgumentException>();
	}

	[Theory(DisplayName = "New File with negative size fails")]
	[AutoDomainData]
	public void NewFileWithNegativeSizeFails(Guid id,
											 string name,
											 string extension,
											 long size,
											 string contentType,
											 bool isTemp,
											 DateTime uploadedOn)
	{
		extension = extension[..File.ExtensionLength];

		var sut = () => FileFactory.CreateMock((id,
												name,
												extension,
												-Math.Abs(size),
												contentType,
												isTemp,
												uploadedOn));
		sut.Should()
		   .Throw<Exception>()
		   .WithInnerException<Exception>()
		   .WithInnerException<ArgumentOutOfRangeException>();
	}

	[Theory(DisplayName = "New File with zero size fails")]
	[AutoDomainData]
	public void NewFileWithZeroSizeFails(Guid id,
										 string name,
										 string extension,
										 string contentType,
										 bool isTemp,
										 DateTime uploadedOn)
	{
		extension = extension[..File.ExtensionLength];

		var sut = () => FileFactory.CreateMock((id,
												name,
												extension,
												0,
												contentType,
												isTemp,
												uploadedOn));

		sut.Should()
		   .Throw<Exception>()
		   .WithInnerException<Exception>()
		   .WithInnerException<ArgumentOutOfRangeException>();
	}

	[Theory(DisplayName = "New File with empty content type fails")]
	[AutoDomainData]
	public void NewFileWithEmptyContentTypeFails(Guid id,
												 string name,
												 string extension,
												 long size,
												 bool isTemp,
												 DateTime uploadedOn)
	{
		extension = extension[..File.ExtensionLength];

		var sut = () => FileFactory.CreateMock((id,
												name,
												extension,
												size,
												string.Empty,
												isTemp,
												uploadedOn));

		sut.Should()
		   .Throw<Exception>()
		   .WithInnerException<Exception>()
		   .WithInnerException<ArgumentException>();
	}

	[Theory(DisplayName = "New File with content type too large fails")]
	[AutoDomainData]
	public void NewFileWithContentTypeTooLargeFails(Guid id,
											 string name,
											 string extension,
											 long size,
											 bool isTemp,
											 DateTime uploadedOn)
	{
		extension = extension[..File.ExtensionLength];

		var sut = () => FileFactory.CreateMock((id,
												name,
												extension,
												size,
												new string(It.IsAny<char>(),
														   File.ContentTypeLength + 1),
												isTemp,
												uploadedOn));

		sut.Should()
		   .Throw<Exception>()
		   .WithInnerException<Exception>()
		   .WithInnerException<ArgumentException>();
	}

	[Theory(DisplayName = "Temp File made permanent succeeds")]
	[AutoDomainData]
	public void TempFileMadePermanentSucceeds(Guid id,
											  string name,
											  string extension,
											  long size,
											  string contentType,
											  DateTime uploadedOn)
	{
		extension = extension[..File.ExtensionLength];
		const bool isTemp = true;

		var sut = FileFactory.CreateMock((id,
										  name,
										  extension,
										  size,
										  contentType,
										  isTemp,
										  uploadedOn));

		sut.IsTemp
		   .Should()
		   .BeTrue();

		sut.MakePermanent();

		sut.IsTemp
		   .Should()
		   .BeFalse();
	}
}