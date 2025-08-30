using AutoFixture;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Monaco.Template.Backend.Application.Features.File;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.File;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - File", "Create")]
public class CreateFileValidatorTests
{
	private static readonly CreateFile.Command Command;

	static CreateFileValidatorTests()
	{
		var fixture = new Fixture();
		Command = new(new MemoryStream([..Encoding.UTF8.GetBytes(fixture.Create<string>())]),									// Stream
					  $"{fixture.Create<string>()}.{fixture.Create<string>()[..(Domain.Model.Entities.File.ExtensionLength - 1)]}",		// FileName
					  fixture.Create<string>());																				// ContentType
	}

	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new CreateFile.Validator();

		sut.RuleLevelCascadeMode
		   .Should()
		   .Be(CascadeMode.Stop);
	}

	[Fact(DisplayName = "File upload valid does not generate validation error")]
	public async Task FileUploadValidDoesNotGenerateError()
	{
		var sut = new CreateFile.Validator();
		var validationResult = await sut.TestValidateAsync(Command);

		validationResult.ShouldNotHaveAnyValidationErrors();
	}

	[Fact(DisplayName = "Stream empty generates validation error")]
	public async Task StreamEmptyGeneratesError()
	{
		var command = Command with { Stream = new MemoryStream() };

		var sut = new CreateFile.Validator();
		var validationResult = await sut.TestValidateAsync(command);

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd)
						.WithErrorCode("PredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "File Name empty generates validation error")]
	public async Task FileNameEmptyGeneratesError()
	{
		var command = Command with { FileName = string.Empty };

		var sut = new CreateFile.Validator();
		var validationResult = await sut.TestValidateAsync(command);

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd)
						.WithErrorCode("PredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "File Name too long generates validation error")]
	public async Task FileNameTooLongGeneratesError()
	{
		var command = Command with { FileName = new string(It.IsAny<char>(), Domain.Model.Entities.File.NameLength + 1) };

		var sut = new CreateFile.Validator();
		var validationResult = await sut.TestValidateAsync(command);

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd)
						.WithErrorCode("PredicateValidator")
						.Should()
						.HaveCount(1);
	}
	
	[Theory(DisplayName = "File Extension too long generates validation error")]
	[AutoDomainData]
	public async Task FileExtensionTooLongGeneratesError(string fileName, string fileExtension)
	{
		var command = Command with { FileName = $"{fileName}.{fileExtension}" };

		var sut = new CreateFile.Validator();
		var validationResult = await sut.TestValidateAsync(command);

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd)
						.WithErrorCode("PredicateValidator")
						.Should()
						.HaveCount(1);
	}

	[Fact(DisplayName = "Content Type too long generates validation error")]
	public async Task ContentTypeTooLongGeneratesError()
	{
		var command = Command with { ContentType = new string(It.IsAny<char>(), Domain.Model.Entities.File.ContentTypeLength + 1) };

		var sut = new CreateFile.Validator();
		var validationResult = await sut.TestValidateAsync(command);

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd)
						.WithErrorCode("PredicateValidator")
						.Should()
						.HaveCount(1);
	}
}