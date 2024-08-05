using AutoFixture;
using FluentAssertions;
using Monaco.Template.Backend.Application.Features.File;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Common.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.File;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - File", "Create")]
public class CreateFileHandlerTests
{
	private readonly Mock<IFileService> _fileServiceMock = new();
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly CreateFile.Command Command;

	static CreateFileHandlerTests()
	{
		var fixture = new Fixture();
		Command = new(It.IsAny<Stream>(),			// Stream
					  fixture.Create<string>(),		// FileName
					  fixture.Create<string>());	// ContentType
	}

	[Theory(DisplayName = "Create new File succeeds")]
	[AnonymousData]
	public async Task CreateNewFileSucceeds(Domain.Model.Document file)
	{
		_dbContextMock.CreateAndSetupDbSetMock(Array.Empty<Domain.Model.File>(), out var fileDbSetMock);
		_fileServiceMock.Setup(x => x.UploadAsync(It.IsAny<Stream>(),
												  It.IsAny<string>(),
												  It.IsAny<string>(),
												  It.IsAny<CancellationToken>()))
						.ReturnsAsync(file);
		
		var sut = new CreateFile.Handler(_dbContextMock.Object, _fileServiceMock.Object);
		var result = await sut.Handle(Command, new CancellationToken());

		_fileServiceMock.Verify(x => x.UploadAsync(It.IsAny<Stream>(),
												   It.IsAny<string>(),
												   It.IsAny<string>(),
												   It.IsAny<CancellationToken>()),
								Times.Once);
		fileDbSetMock.Verify(x => x.AddAsync(It.IsAny<Domain.Model.File>(), It.IsAny<CancellationToken>()),
							 Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()),
							  Times.Once);

		result.ValidationResult
			  .IsValid
			  .Should()
			  .BeTrue();
		result.ItemNotFound
			  .Should()
			  .BeFalse();
		result.Result
			  .Should()
			  .Be(file.Id);
	}

	[Theory(DisplayName = "Create new File error deletes uploaded file from store")]
	[AnonymousData]
	public async Task CreateNewFileErrorDeletesFile(Domain.Model.Document file)
	{
		_dbContextMock.CreateAndSetupDbSetMock(Array.Empty<Domain.Model.File>(), out var fileDbSetMock)
					  .Setup(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
					  .Throws<Exception>();
		_fileServiceMock.Setup(x => x.UploadAsync(It.IsAny<Stream>(),
												  It.IsAny<string>(),
												  It.IsAny<string>(),
												  It.IsAny<CancellationToken>()))
						.ReturnsAsync(file);

		var sut = new CreateFile.Handler(_dbContextMock.Object, _fileServiceMock.Object);
		var action = () => sut.Handle(Command, new CancellationToken());

		await action.Should()
					.ThrowAsync<Exception>();

		_fileServiceMock.Verify(x => x.UploadAsync(It.IsAny<Stream>(),
												   It.IsAny<string>(),
												   It.IsAny<string>(),
												   It.IsAny<CancellationToken>()),
								Times.Once);
		fileDbSetMock.Verify(x => x.AddAsync(It.IsAny<Domain.Model.File>(), It.IsAny<CancellationToken>()),
							 Times.Once);
	}
}