using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Infrastructure.Context;
using Monaco.Template.Backend.Common.Tests;
using Moq;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands", "Delete Company")]
public class DeleteCompanyHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly DeleteCompany.Command _command = new(It.IsAny<Guid>());

	[Fact(DisplayName = "Delete company succeeds")]
	public async Task DeleteCompanySucceeds()
	{
		_dbContextMock.CreateEntityMockAndSetupDbSetMock<AppDbContext, Domain.Model.Company>();

		var sut = new DeleteCompany.Handler(_dbContextMock.Object);
		var result = await sut.Handle(_command, new CancellationToken());

		_dbContextMock.Verify(x => x.Set<Domain.Model.Company>().Remove(It.IsAny<Domain.Model.Company>()), Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
		result.ValidationResult.IsValid.Should().BeTrue();
		result.ItemNotFound.Should().BeFalse();
	}
}
