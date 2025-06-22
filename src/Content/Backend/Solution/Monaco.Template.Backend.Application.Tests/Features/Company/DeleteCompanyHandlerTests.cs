using FluentAssertions;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Company", "Delete")]
public class DeleteCompanyHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly DeleteCompany.Command Command = new(Guid.NewGuid());

	[Theory(DisplayName = "Delete company succeeds")]
	[AutoDomainData(true)]
	public async Task DeleteCompanySucceeds(Domain.Model.Entities.Company company)
	{
		_dbContextMock.CreateAndSetupDbSetMock(company);
		
		var sut = new DeleteCompany.Handler(_dbContextMock.Object);

		var result = await sut.Handle(Command, CancellationToken.None);

		_dbContextMock.Verify(x => x.Set<Domain.Model.Entities.Company>(),
							  Times.Once);

		result.Should()
			  .BeEquivalentTo(CommandResult.Success());
	}
}
