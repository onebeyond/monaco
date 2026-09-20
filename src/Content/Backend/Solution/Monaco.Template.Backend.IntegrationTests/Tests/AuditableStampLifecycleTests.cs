using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Net.Mail;
using AutoFixture.Xunit2;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monaco.Template.Backend.Application.Persistence;
using Monaco.Template.Backend.Common.Domain.Model.Contracts;
using Monaco.Template.Backend.Common.Infrastructure.Persistence;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.Domain.Model.ValueObjects;
using Monaco.Template.Backend.IntegrationTests.Infrastructure;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Collection("IntegrationTests")]
[Trait("Integration Tests", "Auditable Stamps")]
public sealed class AuditableStampLifecycleTests : IntegrationTest
{
	private const string StubActor = "subject:stamp-lifecycle-stub";
	private static readonly Guid CompanyAId = Guid.Parse("8CEFE8FA-F747-4A3A-D8C9-08DC18C76CDC");
	private static readonly Guid CompanyBId = Guid.Parse("95DE146B-86E6-461D-99B3-0CFE0FAA2BAB");
	private static readonly Guid SpainId = Guid.Parse("534A826B-70EF-2128-1A4C-52E23B7D5447");
	private static readonly DateTimeOffset SeedCreatedAtUtc = new(2024, 9, 7, 10, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset StampT0 = new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset StampT1 = new(2026, 9, 20, 8, 5, 0, TimeSpan.Zero);
#if (filesSupport)
	private static readonly Guid ProductAId = Guid.Parse("FA934D1C-1E6D-4DD4-ADC2-08DC18C8810C");
	// ProductPicture.PicturesId is unique; this temp image is seeded and not assigned to any product.
	private static readonly Guid UnassignedImageId = Guid.Parse("418293F5-3F77-44D5-9B98-4B6A9677D5C7");
#endif
	private WebApplicationFactory<Api.Program>? _stubActorFactory;

	public AuditableStampLifecycleTests(AppFixture fixture) : base(fixture)
	{ }

#if (apiService && auth)
	protected override bool RequiresAuthentication =>
		false;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
#if (filesSupport)
		// Products.sql already inserts Company A/B with the same ids as Companies.sql.
		await RunScriptAsync(@"Scripts\Products.sql");
#else
		await RunScriptAsync(@"Scripts\Companies.sql");
#endif
	}

	[Theory(DisplayName = "Added owner receives creation stamps and null modification stamps")]
	[AutoData]
	public async Task AddedOwnerReceivesCreationStampsAndNullModificationStamps(string name,
																				MailAddress email,
																				string street,
																				string city,
																				string county,
																				string postCode)
	{
		var db = CreateDb();
		var company = NewCompany(name, email, await LoadSpainAsync(db), street, city, county, postCode);
		db.Set<Company>()
		  .Add(company);

		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await ReloadCompanyAsync(company.Id);
		AssertCreationOnly(persisted, StubActor);
		persisted.CreatedAtUtc
				 .Offset
				 .Should()
				 .Be(TimeSpan.Zero);
	}

#if (filesSupport)
	[Theory(DisplayName = "Added File TPH owner receives creation stamps only")]
	[AutoData]
	public async Task AddedFileTphOwnerReceivesCreationStampsOnly(string name)
	{
		var db = CreateDb();
		var document = new Document(Guid.NewGuid(),
									name,
									".pdf",
									100,
									"application/pdf",
									false);
		db.Set<Domain.Model.Entities.File>()
		  .Add(document);

		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
									 .Set<Document>()
									 .AsNoTracking()
									 .SingleAsync(file => file.Id == document.Id);
		AssertCreationOnly(persisted, StubActor);
	}
#endif

	[Fact(DisplayName = "Qualifying scalar change restamps modification fields and preserves creation")]
	public async Task QualifyingScalarChangeRestampsModificationAndPreservesCreation()
	{
		var db = CreateDb();
		var company = await db.Set<Company>()
							  .SingleAsync(item => item.Id == CompanyAId);
		db.Entry(company)
		  .Property(nameof(Company.Name))
		  .CurrentValue = $"Renamed-{Guid.NewGuid():N}";

		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await ReloadCompanyAsync(CompanyAId);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(SeedCreatedAtUtc);
		persisted.CreatedBy
				 .Should()
				 .BeNull();
		persisted.ModifiedAtUtc
				 .Should()
				 .NotBeNull();
		persisted.ModifiedBy
				 .Should()
				 .Be(StubActor);
		persisted.ModifiedAtUtc!.Value
				 .Offset
				 .Should()
				 .Be(TimeSpan.Zero);
	}

#if (filesSupport)
	[Fact(DisplayName = "Mapped foreign-key scalar change restamps the owner")]
	public async Task MappedForeignKeyScalarChangeRestampsTheOwner()
	{
		var db = CreateDb();
		var tracked = await db.Set<Product>()
							  .Include(item => item.Company)
							  .SingleAsync(item => item.Id == ProductAId);
		var other = await db.Set<Company>()
							.SingleAsync(item => item.Id == CompanyBId);
		tracked.Update(tracked.Title, tracked.Description, tracked.Price, other);
		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
									 .Set<Product>()
									 .AsNoTracking()
									 .SingleAsync(item => item.Id == ProductAId);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(SeedCreatedAtUtc);
		persisted.CreatedBy
				 .Should()
				 .BeNull();
		persisted.CompanyId
				 .Should()
				 .Be(CompanyBId);
		persisted.ModifiedAtUtc
				 .Should()
				 .NotBeNull();
		persisted.ModifiedBy
				 .Should()
				 .Be(StubActor);
	}
#endif

	[Fact(DisplayName = "Owned dependent change restamps the owner and does not stamp owned values")]
	public async Task OwnedDependentChangeRestampsTheOwnerAndDoesNotStampOwnedValues()
	{
		var db = CreateDb();
		var company = await db.Set<Company>()
							  .SingleAsync(item => item.Id == CompanyAId);
		db.Entry(company)
		  .Reference(item => item.Address)
		  .TargetEntry!
		  .Property(nameof(Address.Street))
		  .CurrentValue = "Owned-Street";

		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await ReloadCompanyAsync(CompanyAId);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(SeedCreatedAtUtc);
		persisted.CreatedBy
				 .Should()
				 .BeNull();
		persisted.Address!.Street
				 .Should()
				 .Be("Owned-Street");
		persisted.ModifiedAtUtc
				 .Should()
				 .NotBeNull();
		persisted.ModifiedBy
				 .Should()
				 .Be(StubActor);
	}

	[Theory(DisplayName = "Owned add on an otherwise ineligible owner restamps the owner")]
	[AutoData]
	public async Task OwnedAddOnOtherwiseIneligibleOwnerRestampsTheOwner(string name,
																		 MailAddress email,
																		 string street,
																		 string city,
																		 string county,
																		 string postCode)
	{
		var db = CreateDb();
		var company = new Company(name, email.Address, null, null);
		db.Set<Company>()
		  .Add(company);
		await db.SaveEntitiesAsync(CancellationToken.None);
		var createdAt = (await ReloadCompanyAsync(company.Id)).CreatedAtUtc;

		db = CreateDb();
		var tracked = await db.Set<Company>()
							  .SingleAsync(item => item.Id == company.Id);
		var addressCountry = await LoadSpainAsync(db);
		tracked.Update(tracked.Name,
					   tracked.Email,
					   tracked.WebSiteUrl,
					   new Address(street, city, county, postCode[..Address.PostCodeLength], addressCountry));
		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await ReloadCompanyAsync(company.Id);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(createdAt);
		persisted.Address
				 .Should()
				 .NotBeNull();
		persisted.ModifiedAtUtc
				 .Should()
				 .NotBeNull();
		persisted.ModifiedBy
				 .Should()
				 .Be(StubActor);
	}

	[Fact(DisplayName = "Owned delete on an otherwise ineligible owner restamps the owner")]
	public async Task OwnedDeleteOnOtherwiseIneligibleOwnerRestampsTheOwner()
	{
		var db = CreateDb();
		var company = await db.Set<Company>()
							  .SingleAsync(item => item.Id == CompanyAId);
		company.Update(company.Name, company.Email, company.WebSiteUrl, null);
		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await ReloadCompanyAsync(CompanyAId);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(SeedCreatedAtUtc);
		persisted.CreatedBy
				 .Should()
				 .BeNull();
		persisted.Address
				 .Should()
				 .BeNull();
		persisted.ModifiedAtUtc
				 .Should()
				 .NotBeNull();
		persisted.ModifiedBy
				 .Should()
				 .Be(StubActor);
	}

#if (filesSupport)
	[Fact(DisplayName = "Skip-navigation change does not restamp the owner")]
	public async Task SkipNavigationChangeDoesNotRestampTheOwner()
	{
		var db = CreateDb();
		var tracked = await db.Set<Product>()
							  .Include(item => item.Pictures)
							  .SingleAsync(item => item.Id == ProductAId);
		var extra = await db.Set<Image>()
							.SingleAsync(item => item.Id == UnassignedImageId);
		tracked.AddPicture(extra);
		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
									 .Set<Product>()
									 .AsNoTracking()
									 .SingleAsync(item => item.Id == ProductAId);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(SeedCreatedAtUtc);
		persisted.CreatedBy
				 .Should()
				 .BeNull();
		persisted.ModifiedAtUtc
				 .Should()
				 .BeNull();
		persisted.ModifiedBy
				 .Should()
				 .BeNull();
	}
#endif

	[Fact(DisplayName = "Stamp-only IsModified does not restamp")]
	public async Task StampOnlyIsModifiedDoesNotRestamp()
	{
		var db = CreateDb();
		var company = await db.Set<Company>()
							  .SingleAsync(item => item.Id == CompanyAId);
		db.Entry(company)
		  .Property(nameof(Company.CreatedBy))
		  .IsModified = true;

		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await ReloadCompanyAsync(CompanyAId);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(SeedCreatedAtUtc);
		persisted.CreatedBy
				 .Should()
				 .BeNull();
		persisted.ModifiedAtUtc
				 .Should()
				 .BeNull();
		persisted.ModifiedBy
				 .Should()
				 .BeNull();
	}

	[Fact(DisplayName = "Unchanged owner is not restamped")]
	public async Task UnchangedOwnerIsNotRestamped()
	{
		var db = CreateDb();
		_ = await db.Set<Company>()
					.SingleAsync(item => item.Id == CompanyAId);

		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await ReloadCompanyAsync(CompanyAId);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(SeedCreatedAtUtc);
		persisted.ModifiedAtUtc
				 .Should()
				 .BeNull();
		persisted.ModifiedBy
				 .Should()
				 .BeNull();
	}

	[Fact(DisplayName = "Tracked deleted owner is not restamped")]
	public async Task TrackedDeletedOwnerIsNotRestamped()
	{
		var db = CreateDb();
#if (filesSupport)
		// ProductPicture is ClientCascade, so Company A's product graph must be tracked for the delete to succeed.
		var company = await db.Set<Company>()
							  .Include(item => item.Products)
							  .ThenInclude(item => item.Pictures)
							  .SingleAsync(item => item.Id == CompanyAId);
#else
		var company = await db.Set<Company>()
							  .SingleAsync(item => item.Id == CompanyAId);
#endif
		db.Set<Company>()
		  .Remove(company);

		await db.SaveEntitiesAsync(CancellationToken.None);

		company.CreatedAtUtc
			   .Should()
			   .Be(SeedCreatedAtUtc);
		company.ModifiedAtUtc
			   .Should()
			   .BeNull();
		company.ModifiedBy
			   .Should()
			   .BeNull();
		(await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
					  .Set<Company>()
					  .AsNoTracking()
					  .SingleOrDefaultAsync(item => item.Id == CompanyAId)).Should()
																		   .BeNull();
	}

	[Theory(DisplayName = "Failed save leaves no durable stamp")]
	[AutoData]
	public async Task FailedSaveLeavesNoDurableStamp(string name,
													 MailAddress email,
													 string street,
													 string city,
													 string county,
													 string postCode)
	{
		await using var factory = CreateFactory(extraInterceptor: new ThrowOnWriteDbCommandInterceptor());
		var db = Fixture.GetDbContext(factory.Services);
		var company = NewCompany(name, email, await LoadSpainAsync(db), street, city, county, postCode);
		db.Set<Company>()
		  .Add(company);

		var act = async () => await db.SaveEntitiesAsync(CancellationToken.None);
		(await act.Should()
				  .ThrowAsync<DbUpdateException>())
			.Which
			.InnerException
			.Should()
			.BeOfType<InvalidOperationException>();
		company.CreatedAtUtc
			   .Should()
			   .NotBe(default);

		(await Fixture.GetDbContext(factory.Services)
					  .Set<Company>()
					  .AsNoTracking()
					  .SingleOrDefaultAsync(item => item.Id == company.Id)).Should()
																		   .BeNull();
	}

	[Theory(DisplayName = "Execution-strategy retry without interceptor re-entry retains pending stamps")]
	[AutoData]
	public async Task ExecutionStrategyRetryWithoutReentryRetainsPendingStamps(string name,
																			   MailAddress email,
																			   string street,
																			   string city,
																			   string county,
																			   string postCode)
	{
		var time = new SequenceTimeProvider(StampT0);
		await using var factory = CreateFactory(time, new TransientOnceDbCommandInterceptor());
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var company = NewCompany(name, email, await LoadSpainAsync(db), street, city, county, postCode);
		db.Set<Company>()
		  .Add(company);

		await db.SaveEntitiesAsync(CancellationToken.None);

		time.GetUtcNowCalls
			.Should()
			.Be(1);
		var persisted = await Fixture.GetDbContext(factory.Services)
									 .Set<Company>()
									 .AsNoTracking()
									 .SingleAsync(item => item.Id == company.Id);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(StampT0);
		persisted.CreatedBy
				 .Should()
				 .Be(StubActor);
	}

	[Theory(DisplayName = "New SavingChanges after tracker clear resolves fresh values")]
	[AutoData]
	public async Task NewSavingChangesAfterTrackerClearResolvesFreshValues(string name,
																		   MailAddress email,
																		   string street,
																		   string city,
																		   string county,
																		   string postCode)
	{
		var time = new SequenceTimeProvider(StampT0, StampT1);
		await using var factory = CreateFactory(time);
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var company = NewCompany(name, email, await LoadSpainAsync(db), street, city, county, postCode);
		db.Set<Company>()
		  .Add(company);
		await db.SaveEntitiesAsync(CancellationToken.None);

		db.ChangeTracker.Clear();
		var tracked = await db.Set<Company>()
							  .SingleAsync(item => item.Id == company.Id);
		db.Entry(tracked)
		  .Property(nameof(Company.Name))
		  .CurrentValue = $"Retry-{Guid.NewGuid():N}";
		await db.SaveEntitiesAsync(CancellationToken.None);

		time.GetUtcNowCalls
			.Should()
			.Be(2);
		var persisted = await Fixture.GetDbContext(factory.Services)
									 .Set<Company>()
									 .AsNoTracking()
									 .SingleAsync(item => item.Id == company.Id);
		persisted.CreatedAtUtc
				 .Should()
				 .Be(StampT0);
		persisted.ModifiedAtUtc
				 .Should()
				 .Be(StampT1);
	}

	[Theory(DisplayName = "Telemetry signals do not change stamp eligibility or values")]
	[AutoData]
	public async Task TelemetrySignalsDoNotChangeStampEligibilityOrValues(string name,
																		  MailAddress email,
																		  string street,
																		  string city,
																		  string county,
																		  string postCode)
	{
		using var activity = new Activity("tel-01").Start();
		activity.SetTag("user.id", "trace-user");
		activity.SetTag("service.name", "trace-service");
		activity.SetBaggage("user.id", "baggage-user");
		activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
		using var listener = new MeterListener();
		listener.Start();
		using var meter = new Meter("stamp-lifecycle-tel");
		meter.CreateCounter<long>("stamp.tel")
			 .Add(1);

		var db = CreateDb();
		var company = NewCompany(name, email, await LoadSpainAsync(db), street, city, county, postCode);
		db.Set<Company>()
		  .Add(company);
		await db.SaveEntitiesAsync(CancellationToken.None);

		var persisted = await ReloadCompanyAsync(company.Id);
		persisted.CreatedBy
				 .Should()
				 .Be(StubActor);
		persisted.CreatedBy
				 .Should()
				 .NotBe("trace-user")
				 .And
				 .NotBe("baggage-user");
		persisted.ModifiedAtUtc
				 .Should()
				 .BeNull();
	}

	[Theory(DisplayName = "Sync and async SavingChanges apply identical stamps")]
	[AutoData]
	public async Task SyncAndAsyncSavingChangesApplyIdenticalStamps(string name,
																	MailAddress email,
																	string otherName,
																	MailAddress otherEmail,
																	string street,
																	string city,
																	string county,
																	string postCode)
	{
		var time = new SequenceTimeProvider(StampT0);
		await using var factory = CreateFactory(time);
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var country = await LoadSpainAsync(db);
		var first = NewCompany(name, email, country, street, city, county, postCode);
		db.Set<Company>()
		  .Add(first);
		db.SaveChanges();

		var second = NewCompany(otherName, otherEmail, country, street, city, county, postCode);
		db.Set<Company>()
		  .Add(second);
		await db.SaveChangesAsync();

		var persisted = await Fixture.GetDbContext(factory.Services)
									 .Set<Company>()
									 .AsNoTracking()
									 .Where(item => item.Id == first.Id || item.Id == second.Id)
									 .ToListAsync();
		persisted.Should()
				 .HaveCount(2);
		persisted.Should()
				 .AllSatisfy(item =>
							 {
								 item.CreatedAtUtc
									 .Should()
									 .Be(StampT0);
								 item.CreatedBy
									 .Should()
									 .Be(StubActor);
								 item.ModifiedAtUtc
									 .Should()
									 .BeNull();
							 });
	}

	[Theory(DisplayName = "One SavingChanges invocation reuses one UTC and one actor")]
	[AutoData]
	public async Task OneSavingChangesInvocationReusesOneUtcAndOneActor(string name,
																		MailAddress email,
																		string otherName,
																		MailAddress otherEmail,
																		string street,
																		string city,
																		string county,
																		string postCode)
	{
		var time = new SequenceTimeProvider(StampT0, StampT1);
		await using var factory = CreateFactory(time);
		using var scope = factory.Services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
		var country = await LoadSpainAsync(db);
		var first = NewCompany(name, email, country, street, city, county, postCode);
		var second = NewCompany(otherName, otherEmail, country, street, city, county, postCode);
		db.Set<Company>()
		  .AddRange(first, second);

		await db.SaveEntitiesAsync(CancellationToken.None);

		time.GetUtcNowCalls
			.Should()
			.Be(1);
		first.CreatedAtUtc
			 .Should()
			 .Be(second.CreatedAtUtc)
			 .And
			 .Be(StampT0);
		first.CreatedBy
			 .Should()
			 .Be(second.CreatedBy);
	}

	public override async Task DisposeAsync()
	{
		if (_stubActorFactory is not null)
			await _stubActorFactory.DisposeAsync();
		await base.DisposeAsync();
	}

	private AppDbContext CreateDb() =>
		Fixture.GetDbContext(StubActorFactory.Services);

	private WebApplicationFactory<Api.Program> StubActorFactory =>
		_stubActorFactory ??= CreateFactory();

	private WebApplicationFactory<Api.Program> CreateFactory(TimeProvider? timeProvider = null, IInterceptor? extraInterceptor = null) =>
		Fixture.WebAppFactory.GetCustomFactory(builder =>
												   builder.ConfigureServices(services =>
																			 {
																				 services.RemoveAll<IPersistenceActorProvider>();
																				 services.AddScoped<IPersistenceActorProvider, StubPersistenceActorProvider>();

																				 if (timeProvider is not null)
																				 {
																					 services.RemoveAll<TimeProvider>();
																					 services.AddSingleton(timeProvider);
																				 }

																				 if (extraInterceptor is not null)
																					 services.ConfigureDbContext<AppDbContext>((_, opts) => opts.AddInterceptors(extraInterceptor));
																			 }));

	private async Task<Company> ReloadCompanyAsync(Guid id) =>
		await Fixture.GetDbContext(Fixture.WebAppFactory.Services)
					 .Set<Company>()
					 .AsNoTracking()
					 .SingleAsync(item => item.Id == id);

	private static async Task<Country> LoadSpainAsync(AppDbContext db) =>
		await db.Set<Country>()
				.SingleAsync(item => item.Id == SpainId);

	private static Company NewCompany(string name,
									  MailAddress email,
									  Country country,
									  string street,
									  string city,
									  string county,
									  string postCode) =>
		new(name,
			email.Address,
			null,
			new Address(street, city, county, postCode[..Address.PostCodeLength], country));

	private static void AssertCreationOnly(IAuditable entity, string actor)
	{
		entity.CreatedAtUtc
			  .Should()
			  .NotBe(default);
		entity.CreatedBy
			  .Should()
			  .Be(actor);
		entity.ModifiedAtUtc
			  .Should()
			  .BeNull();
		entity.ModifiedBy
			  .Should()
			  .BeNull();
	}

	private sealed class StubPersistenceActorProvider : IPersistenceActorProvider
	{
		public string HostRole =>
			PersistenceActor.ApiHostRole;

		public string? GetActor() =>
			StubActor;
	}

	private sealed class SequenceTimeProvider : TimeProvider
	{
		private readonly DateTimeOffset[] _values;
		private int _index;

		public SequenceTimeProvider(params DateTimeOffset[] values) =>
			_values = values;

		public int GetUtcNowCalls { get; private set; }

		public override DateTimeOffset GetUtcNow()
		{
			GetUtcNowCalls++;
			var index = Math.Min(_index, _values.Length - 1);
			if (_index < _values.Length - 1)
				_index++;
			return _values[index];
		}
	}
}
