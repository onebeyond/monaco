#if (filesSupport)
using AutoFixture;
#endif
using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Infrastructure.Context;
#if (filesSupport)
using Monaco.Template.Backend.Application.Services.Contracts;
#endif
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Common.Tests.Factories;
#if (filesSupport)
using Monaco.Template.Backend.Domain.Model;
#endif
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Company", "Delete")]
public class DeleteCompanyHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly DeleteCompany.Command Command = new(It.IsAny<Guid>());
	#if (filesSupport)
	private readonly Mock<IFileService> _fileServiceMock = new();
	#endif

	[Theory(DisplayName = "Delete company succeeds")]
	[AnonymousData(true)]
	#if (filesSupport)
	public async Task DeleteCompanySucceeds(IFixture fixture, Domain.Model.Product[] products)
	#else
	public async Task DeleteCompanySucceeds(Domain.Model.Company company)
	#endif
	{
		#if (filesSupport)
		var companyMock = new Mock<Domain.Model.Company>(fixture.Create<string>(),
														 fixture.Create<string>(),
														 fixture.Create<string>(),
														 fixture.Create<Address>());
		companyMock.SetupGet(x => x.Id)
				   .Returns(Guid.NewGuid());
		companyMock.SetupGet(x => x.Products)
				   .Returns(products);
		
		var pictures = products.SelectMany(x => x.Pictures)
							   .Union(products.SelectMany(x => x.Pictures.Select(p => p.Thumbnail!)))
							   .ToArray();

		_dbContextMock.CreateAndSetupDbSetMock(companyMock.Object, out var companyDbSetMock);
		_dbContextMock.CreateAndSetupDbSetMock(pictures, out var imageDbSetMock);
		#else
		_dbContextMock.CreateAndSetupDbSetMock(company, out var companyDbSetMock);
		#endif

		var command = Command with
					  {
						  #if (filesSupport)
						  Id = companyMock.Object.Id
						  #else
						  Id = company.Id
						  #endif
					  };
		
		#if (filesSupport)
		var sut = new DeleteCompany.Handler(_dbContextMock.Object, _fileServiceMock.Object);
		#else
		var sut = new DeleteCompany.Handler(_dbContextMock.Object);
		#endif
		var result = await sut.Handle(command, new CancellationToken());

		companyDbSetMock.Verify(x => x.Remove(It.IsAny<Domain.Model.Company>()),
								Times.Once);
		#if (filesSupport)
		imageDbSetMock.Verify(x => x.RemoveRange(It.IsAny<IEnumerable<Image>>()),
							  Times.Once);
		#endif
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()),
							  Times.Once);
		#if (filesSupport)
		_fileServiceMock.Verify(x => x.DeleteImagesAsync(It.IsAny<Image[]>(), It.IsAny<CancellationToken>()),
								Times.Once);
		#endif

		result.ValidationResult
			  .IsValid
			  .Should()
			  .BeTrue();

		result.ItemNotFound
			  .Should()
			  .BeFalse();
	}
}
