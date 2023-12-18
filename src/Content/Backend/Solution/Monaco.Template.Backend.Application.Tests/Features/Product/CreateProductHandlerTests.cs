using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Application.Services.Contracts;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Common.Tests.Factories;
using Monaco.Template.Backend.Domain.Model;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Product", "Create")]
public class CreateProductHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private readonly Mock<IFileService> _fileServiceMock = new();
	private static readonly CreateProduct.Command Command = new(It.IsAny<string>(),     // Title
																It.IsAny<string>(),     // Description
																It.IsAny<decimal>(),    // Price
																It.IsAny<Guid>(),       // CompanyId
																It.IsAny<Guid[]>(),     // Pictures
																It.IsAny<Guid>());      // DefaultPictureId


	[Theory(DisplayName = "Create new Product succeeds")]
	[AnonymousData]
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

		var sut = new CreateProduct.Handler(_dbContextMock.Object, _fileServiceMock.Object);
		var result = await sut.Handle(command, new CancellationToken());

		productDbSetMock.Verify(x => x.Attach(It.IsAny<Domain.Model.Product>()), Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
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