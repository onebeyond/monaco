using AwesomeAssertions;
using Monaco.Template.Backend.Domain.Model.ValueObjects;
using Monaco.Template.Backend.Domain.Tests.Factories;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "ImageDimensions Entity")]
public class ImageDimensionsTests
{
	[Theory(DisplayName = "New ImageDimensions succeeds")]
	[AutoDomainData]
	public void NewImageDimensionsSucceeds(int height, int width)
	{
		var sut = new ImageDimensions(height, width);

		sut.Height
		   .Should()
		   .Be(height);
		sut.Width
		   .Should()
		   .Be(width);
	}

	[Theory(DisplayName = "New ImageDimensions with negative values throws")]
	[InlineData(-1, 1)]
	[InlineData(1, -1)]
	public void NewImageDimensionsWithNegativeValuesThrows(int height, int width)
	{
		var sut = () => new ImageDimensions(height, width);

		sut.Should()
		   .ThrowExactly<ArgumentOutOfRangeException>();
	}
}