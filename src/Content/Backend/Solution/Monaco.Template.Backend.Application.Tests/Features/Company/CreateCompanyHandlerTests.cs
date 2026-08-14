using AutoFixture;
using AwesomeAssertions;
using Monaco.Template.Backend.Application.Diagnostics;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Tests;
using Monaco.Template.Backend.Domain.Tests.Factories;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using Xunit;

namespace Monaco.Template.Backend.Application.Tests.Features.Company;

[ExcludeFromCodeCoverage]
[Trait("Application Commands - Company", "Create")]
public class CreateCompanyHandlerTests
{
	private readonly Mock<AppDbContext> _dbContextMock = new();
	private static readonly CreateCompany.Command Command;

	static CreateCompanyHandlerTests()
	{
		var fixture = new Fixture();
		Command = new(fixture.Create<string>(), // Name
					  fixture.Create<string>(), // Email
					  fixture.Create<string>(), // WebsiteUrl
					  fixture.Create<string>(), // Street
					  fixture.Create<string>(), // City
					  fixture.Create<string>(), // County
					  fixture.Create<string>(), // PostCode
					  fixture.Create<Guid>()); // CountryId
	}

	[Theory(DisplayName = "Create new company succeeds")]
	[AutoDomainData]
	public async Task CreateNewCompanySucceeds(Domain.Model.Entities.Country country)
	{
		var measurements = new List<(long Value, int TagCount)>();
		using var listener = CreateSuccessfulCompanyListener(measurements);

		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Entities.Company>(), out var companyDbSetMock)
					  .CreateAndSetupDbSetMock([country]);

		var sut = new CreateCompany.Handler(_dbContextMock.Object, NullLogger<CreateCompany.Handler>.Instance);
		var result = await sut.Handle(Command, CancellationToken.None);

		companyDbSetMock.Verify(x => x.Add(It.IsAny<Domain.Model.Entities.Company>()), Times.Once);
		_dbContextMock.Verify(x => x.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);

		var success = result.Should()
							.BeOfType<Success<Guid>>();
		success.Subject
			   .Result
			   .Should()
			   .NotBeEmpty();

		measurements.Should()
					.ContainSingle()
					.Which
					.Should()
					.Be((1L, 0));
	}

	[Fact(DisplayName = "Successful company counter has the required BCL metadata")]
	public Task SuccessfulCompanyCounterHasRequiredMetadata()
	{
		Instrument? publishedInstrument = null;
		using var listener = new MeterListener
		{
			InstrumentPublished = (instrument, meterListener) =>
						  {
							  if (instrument.Meter.Name == ApplicationDiagnostics.MeterName && instrument.Name == "company.successful_creations")
							  {
								  publishedInstrument = instrument;
								  meterListener.EnableMeasurementEvents(instrument);
							  }
						  }
		};

		listener.Start();
		_ = ApplicationDiagnostics.SuccessfulCompanyCreations;

		var counter = Assert.IsType<Counter<long>>(publishedInstrument);
		Assert.Equal("Monaco.Template.Backend.Application", counter.Meter.Name);
		Assert.Equal("company.successful_creations", counter.Name);
		Assert.Equal("{company}", counter.Unit);
		Assert.Equal("The number of companies successfully created after the Company unit of work commits.", counter.Description);
		return Task.CompletedTask;
	}

	[Theory(DisplayName = "Persistence failure does not record a successful company counter measurement")]
	[AutoDomainData]
	public async Task PersistenceFailureDoesNotRecordSuccessfulCompanyMeasurement(Domain.Model.Entities.Country country)
	{
		var measurements = new List<(long Value, int TagCount)>();
		using var listener = CreateSuccessfulCompanyListener(measurements);

		_dbContextMock.CreateAndSetupDbSetMock(new List<Domain.Model.Entities.Company>())
					  .CreateAndSetupDbSetMock([country]);
		_dbContextMock.Setup(context => context.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
					  .ThrowsAsync(new InvalidOperationException("Persistence failed."));

		var sut = new CreateCompany.Handler(_dbContextMock.Object, NullLogger<CreateCompany.Handler>.Instance);

		await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Handle(Command, CancellationToken.None));

		measurements.Should().BeEmpty();
	}

	private static MeterListener CreateSuccessfulCompanyListener(List<(long Value, int TagCount)> measurements)
	{
		var listener = new MeterListener
		{
			InstrumentPublished = (instrument, meterListener) =>
								  {
									  if (instrument.Meter.Name == ApplicationDiagnostics.MeterName && instrument.Name == "company.successful_creations")
										  meterListener.EnableMeasurementEvents(instrument);
								  }
		};

		listener.SetMeasurementEventCallback<long>((_, value, tags, _) => measurements.Add((value, tags.Length)));
		listener.Start();
		return listener;
	}
}