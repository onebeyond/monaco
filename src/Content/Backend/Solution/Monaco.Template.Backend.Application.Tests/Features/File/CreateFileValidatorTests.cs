using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Monaco.Template.Backend.Application.Features.File;
using Moq;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.File;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - File", "Create")]
public class CreateFileValidatorTests
{
	private static readonly CreateFile.Command Command = new(It.IsAny<Stream>(),	// Stream
															 It.IsAny<string>(),	// FileName
															 It.IsAny<string>());   // ContentType

	[Fact(DisplayName = "Validator's rule level cascade mode is 'Stop'")]
	public void ValidatorRuleLevelCascadeModeIsStop()
	{
		var sut = new CreateFile.Validator();

		sut.RuleLevelCascadeMode
		   .Should()
		   .Be(CascadeMode.Stop);
	}

	[Fact(DisplayName = "Stream being valid does not generate validation error")]
	public async Task StreamDoesNotGenerateErrorWhenValid()
	{
		var command = Command with
					  {
						  Stream = new MemoryStream("Content"u8.ToArray())
					  };

		var sut = new CreateFile.Validator();
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Stream));

		validationResult.ShouldNotHaveValidationErrorFor(cmd => cmd.Stream);
	}

	[Fact(DisplayName = "Stream empty generates validation error")]
	public async Task StreamEmptyGeneratesError()
	{
		var command = Command with
					  {
						  Stream = new MemoryStream()
					  };

		var sut = new CreateFile.Validator();
		var validationResult = await sut.TestValidateAsync(command, strategy => strategy.IncludeProperties(cmd => cmd.Stream));

		validationResult.ShouldHaveValidationErrorFor(cmd => cmd.Stream)
						.WithErrorCode("PredicateValidator")
						.Should()
						.HaveCount(1);
	}
}