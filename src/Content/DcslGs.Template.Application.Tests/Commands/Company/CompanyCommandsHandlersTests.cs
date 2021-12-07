using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DcslGs.Template.Application.Commands.Company;
using DcslGs.Template.Common.Tests.Factories;
using DcslGs.Template.Domain.Model;
using DcslGs.Template.Infrastructure.Context;
using Xunit;

namespace DcslGs.Template.Application.Tests.Commands.Company;

[ExcludeFromCodeCoverage]
public class CompanyCommandsHandlersTests
{
    [Trait("Application Commands", "Company Commands")]
    [Theory(DisplayName = "Create new company succeeds")]
    [AnonymousData]
    public async Task CreateNewCompanySucceeds(Country country)
    {
        var dbContextMock = new Mock<AppDbContext>();
        var companyDbSetMock = new List<Domain.Model.Company>().AsQueryable().BuildMockDbSet();
        dbContextMock.Setup(x => x.Set<Domain.Model.Company>())
                     .Returns(companyDbSetMock.Object);
        var countryDbSetMock = new[] { country }.AsQueryable().BuildMockDbSet();
        dbContextMock.Setup(x => x.Set<Country>())
                     .Returns(countryDbSetMock.Object);
        var commandMock = new Mock<CompanyCreateCommand>();
			
        var sut = new CompanyCommandsHandlers(dbContextMock.Object);
        var result = await sut.Handle(commandMock.Object, new CancellationToken());

        companyDbSetMock.Verify(x => x.Attach(It.IsAny<Domain.Model.Company>()), Times.Once);
        dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.ValidationResult.IsValid.Should().BeTrue();
        result.ItemNotFound.Should().BeFalse();
    }

    [Trait("Application Commands", "Company Commands")]
    [Theory(DisplayName = "Edit company succeeds")]
    [AnonymousData]
    public async Task EditCompanySucceeds(Country country)
    {
        var companyMock = new Mock<Domain.Model.Company>();
        var dbContextMock = new Mock<AppDbContext>();
        var companyDbSetMock = new List<Domain.Model.Company> {companyMock.Object}.AsQueryable().BuildMockDbSet();
        companyDbSetMock.Setup(x => x.FindAsync(new object[] {It.IsAny<Guid>()}, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(companyMock.Object);
        dbContextMock.Setup(x => x.Set<Domain.Model.Company>())
                     .Returns(companyDbSetMock.Object);
        var countryDbSetMock = new[] { country }.AsQueryable().BuildMockDbSet();
        dbContextMock.Setup(x => x.Set<Country>())
                     .Returns(countryDbSetMock.Object);
        var commandMock = new Mock<CompanyEditCommand>();

        var sut = new CompanyCommandsHandlers(dbContextMock.Object);
        var result = await sut.Handle(commandMock.Object, new CancellationToken());

        companyMock.Verify(x => x.Update(It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<string>(),
                                         It.IsAny<Country>()),
                           Times.Once);
        dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.ValidationResult.IsValid.Should().BeTrue();
        result.ItemNotFound.Should().BeFalse();
    }
}