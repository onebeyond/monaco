using Monaco.Template.Backend.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using FluentAssertions;
using Monaco.Template.Backend.Common.Tests.Factories;
using Xunit;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Image Entity")]
public class ImageTests
{
	[Theory(DisplayName = "New Image succeeds")]
	[AnonymousData]
	public void NewImageSucceeds(Guid id,
								 string name,
								 string extension,
								 long size,
								 string contentType,
								 bool isTemp,
								 int height,
								 int width,
								 DateTime dateTaken,
								 Image thumbnail)
	{
		extension = extension[..20];
		var latitude = RandomNumberGenerator.GetInt32(-90, 90);
		var longitude = RandomNumberGenerator.GetInt32(-180, 180);

		var sut = new Image(id,
							name,
							extension,
							size,
							contentType,
							isTemp,
							height,
							width,
							dateTaken,
							latitude,
							longitude,
							thumbnail);

		sut.Id
		   .Should()
		   .Be(id);
		sut.Name
		   .Should()
		   .Be(name);
		sut.Extension
		   .Should()
		   .Be(extension);
		sut.Size
		   .Should()
		   .Be(size);
		sut.ContentType
		   .Should()
		   .Be(contentType);
		sut.IsTemp
		   .Should()
		   .Be(isTemp);
		sut.Dimensions
		   .Should()
		   .NotBeNull();
		sut.Dimensions
		   .Height
		   .Should()
		   .Be(height);
		sut.Dimensions
		   .Width
		   .Should()
		   .Be(width);
		sut.DateTaken
		   .Should()
		   .Be(dateTaken);
		sut.Position
		   .Should()
		   .NotBeNull();
		sut.Position!
		   .Latitude
		   .Should()
		   .Be(latitude);
		sut.Position!
		   .Longitude
		   .Should()
		   .Be(longitude);
		sut.Thumbnail
		   .Should()
		   .Be(thumbnail);
	}
}