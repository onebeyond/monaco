using AutoFixture;
using AwesomeAssertions;
using Monaco.Template.Backend.Application.Features.Product;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Product;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Product", "Edit")]
public class EditProductHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly EditProduct.Command Command;

	static EditProductHandlerTests()
	{
		var fixture = new Fixture();
		Command = new(fixture.Create<Guid>(),		// Id
					  fixture.Create<string>(),		// Title
					  fixture.Create<string>(),		// Description
					  fixture.Create<decimal>(),	// Price
					  fixture.Create<Guid>(),		// CompanyId
					  fixture.Create<Guid[]>(),		// Pictures
					  fixture.Create<Guid>());		// DefaultPictureId
	}
	
	[Theory(DisplayName = "Edit existing Product succeeds")]
	[AutoDomainData(true)]
	public async Task EditexistingProductSucceeds(string existingTitle,
												  string newTitle,
												  string existingDescription,
												  string newDescription,
												  decimal existingPrice,
												  decimal newPrice,
												  Domain.Model.Entities.Company existingCompany,
												  Domain.Model.Entities.Company newCompany,
												  List<Image> existingPictures,
												  List<Image> newPictures)
	{
		var productMock = new Mock<Domain.Model.Entities.Product>(existingTitle,
																  existingDescription,
																  existingPrice,
																  existingCompany,
																  existingPictures,
																  existingPictures.First())
						  {
							  CallBase = true
						  };
		productMock.SetupGet(x => x.Id)
				   .Returns(Guid.NewGuid());
		
		var product = productMock.Object;

		_dbContextMock.CreateAndSetupDbSetMock(product)
					  .CreateAndSetupDbSetMock([existingCompany, newCompany])
					  .CreateAndSetupDbSetMock([..existingPictures, ..newPictures]);

		var command = Command with
					  {
						  Id = product.Id,
						  Title = newTitle,
						  Description = newDescription,
						  Price = newPrice,
						  CompanyId = newCompany.Id,
						  Pictures = [..newPictures.Select(x => x.Id)],
						  DefaultPictureId = newPictures.First().Id
					  };

		var sut = new EditProduct.Handler(_dbContextMock.Object);
		var result = await sut.Handle(command, CancellationToken.None);

		productMock.Verify(x => x.Update(It.IsAny<string>(),
										 It.IsAny<string>(),
										 It.IsAny<decimal>(),
										 It.IsAny<Domain.Model.Entities.Company>()));
		productMock.Verify(x => x.AddPicture(It.IsAny<Image>()), Times.Exactly(6));
		productMock.Verify(x => x.RemovePicture(It.IsAny<Image>()), Times.Exactly(3));
		productMock.Verify(x => x.SetDefaultPicture(It.IsAny<Image>()), Times.Exactly(2));

		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()),
							  Times.Once);
		
		result.Should()
			  .BeEquivalentTo(CommandResult.Success());
	}
}