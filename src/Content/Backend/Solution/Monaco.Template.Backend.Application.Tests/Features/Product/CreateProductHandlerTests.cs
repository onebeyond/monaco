﻿using AutoFixture;
using FluentAssertions;
#if (massTransitIntegration)
using MassTransit;
#endif
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Model;
using Monaco.Template.Backend.Domain.Tests.Factories;
#if (massTransitIntegration)
using Monaco.Template.Backend.Messages.V1;
#endif
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Product", "Create")]
public class CreateProductHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
#if (massTransitIntegration)
	private readonly Mock<IPublishEndpoint> _publishEndpointMock = new();
#endif
	private readonly Mock<IFileService> _fileServiceMock = new();
	private static readonly CreateProduct.Command Command;

	static CreateProductHandlerTests()
	{
		var fixture = new Fixture();
		Command = new(fixture.Create<string>(),		// Title
					  fixture.Create<string>(),		// Description
					  fixture.Create<decimal>(),	// Price
					  fixture.Create<Guid>(),		// CompanyId
					  fixture.Create<Guid[]>(),		// Pictures
					  fixture.Create<Guid>());		// DefaultPictureId
	}


	[Theory(DisplayName = "Create new Product succeeds")]
	[AutoDomainData]
	public async Task CreateNewProductSucceeds(Domain.Model.Company company, Image[] pictures)
	{
		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Product>(), out var productDbSetMock)
					  .CreateAndSetupDbSetMock(company)
					  .CreateAndSetupDbSetMock(pictures);

		var command = Command with
					  {
						  CompanyId = company.Id,
						  Pictures = pictures.Select(x => x.Id)
											 .ToArray(),
						  DefaultPictureId = pictures.First()
													 .Id
					  };

		var sut = new CreateProduct.Handler(_dbContextMock.Object,
#if (massTransitIntegration)
											_publishEndpointMock.Object,
#endif
											_fileServiceMock.Object);
		var result = await sut.Handle(command, new CancellationToken());

		productDbSetMock.Verify(x => x.Attach(It.IsAny<Domain.Model.Product>()), Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
#if (massTransitIntegration)
		_publishEndpointMock.Verify(x => x.Publish(It.IsAny<ProductCreated>(), It.IsAny<CancellationToken>()), Times.Once);
#endif
		_fileServiceMock.Verify(x => x.MakePermanentImagesAsync(It.IsAny<Image[]>(), It.IsAny<CancellationToken>()), Times.Once);

		result.ValidationResult
			  .IsValid
			  .Should()
			  .BeTrue();
		result.ItemNotFound
			  .Should()
			  .BeFalse();
	}
}