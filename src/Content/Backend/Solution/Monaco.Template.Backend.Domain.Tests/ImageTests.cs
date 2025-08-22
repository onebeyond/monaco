using FluentAssertions;
using Monaco.Template.Backend.Domain.Model;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.Domain.Tests.Factories;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Monaco.Template.Backend.Domain.Model.ValueObjects;
using Xunit;
using File = Monaco.Template.Backend.Domain.Model.Entities.File;

namespace Monaco.Template.Backend.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Core Domain Entities", "Image Entity")]
public class ImageTests
{
	[Theory(DisplayName = "New Image succeeds")]
	[AutoDomainData]
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
		extension = extension[..File.ExtensionLength];
		var latitude = RandomNumberGenerator.GetInt32(GpsPosition.LatitudeMin, GpsPosition.LatitudeMax);
		var longitude = RandomNumberGenerator.GetInt32(GpsPosition.LongitudeMin, GpsPosition.LongitudeMax);

		var sut = new Image(id,
							name,
							extension,
							size,
							contentType,
							isTemp,
							(height,
							width),
							dateTaken,
							(latitude,
							longitude),
							(thumbnail.Id,
							 thumbnail.Size,
							 (thumbnail.Dimensions.Width,
							  thumbnail.Dimensions.Height)));

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