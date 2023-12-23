using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "ImageDimensions Entity")]
public class ImageDimensionsTests
{
	[Theory(DisplayName = "New ImageDimensions succeeds")]
	[AnonymousData]
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
}