using AutoFixture.Xunit2;
using AwesomeAssertions;
using Flurl.Http;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Domain.Model.Entities;
using Monaco.Template.Backend.Domain.Model.ValueObjects;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Mail;

namespace Monaco.Template.Backend.IntegrationTests.Tests;

[ExcludeFromCodeCoverage]
[Trait("Integration Tests", "Companies")]
public class CompaniesTests : IntegrationTest
{
	public CompaniesTests(AppFixture fixture) : base(fixture)
	{ }

#if (auth)
	protected override bool RequiresAuthentication => true;
#else
	protected override bool RequiresAuthentication => false;
#endif

	public override async Task InitializeAsync()
	{
		await base.InitializeAsync();
		await RunScriptAsync(@"Scripts\Companies.sql");
#if (auth)

		await SetupAccessToken([Auth.Auth.Roles.Administrator]);
#endif
	}

	[Theory(DisplayName = "Get Companies page succeeds")]
	[InlineData(false, null, null, 3)]
	[InlineData(true, 1, 5, 2)]
	public async Task GetCompaniesPageSucceeds(bool expandCountry,
											   int? offset,
											   int? limit,
											   int expectedItemsCount)
	{
		var response = await CreateRequest(ApiRoutes.Companies.Query(expandCountry, offset, limit)).GetAsync();
		
		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.OK);

		var result = await response.GetJsonAsync<Page<CompanyDto>>();

		result.Should()
			  .NotBeNull();
		result.Items
			  .Should()
			  .HaveCount(expectedItemsCount);
		result.Items
			  .Should()
			  .AllSatisfy(p =>
						  {
							  if (expandCountry)
								  p.Country
								   .Should()
								   .NotBeNull();
							  else
								  p.Country
								   .Should()
								   .BeNull();
						  });
		result.Pager
			  .Should()
			  .BeEquivalentTo(new Pager(offset ?? 0,
										limit ?? 10,
										3));
	}

	[Fact(DisplayName = "Get Company succeeds")]
	public async Task GetCompanySucceeds()
	{
		var companyId = Guid.Parse("8CEFE8FA-F747-4A3A-D8C9-08DC18C76CDC");

		var response = await CreateRequest(ApiRoutes.Companies.Get(companyId)).GetAsync();

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.OK);

		var result = await response.GetJsonAsync<CompanyDto>();
		var company = await GetDbContext().Set<Company>()
										  .SingleAsync(c => c.Id == companyId);

		result.Should()
			  .NotBeNull();
		result.Name
			  .Should()
			  .Be(company.Name);
		result.Email
			  .Should()
			  .Be(company.Email);
		result.WebSiteUrl
			  .Should()
			  .Be(company.WebSiteUrl);
		result.Street
			  .Should()
			  .Be(company.Address!.Street);
		result.City
			  .Should()
			  .Be(company.Address!.City);
		result.County
			  .Should()
			  .Be(company.Address!.County);
		result.PostCode
			  .Should()
			  .Be(company.Address!.PostCode);
		result.CountryId
			  .Should()
			  .Be(company.Address!.CountryId);
	}

	[Theory(DisplayName = "Create new Company succeeds")]
	[AutoData]
	public async Task CreateNewCompanySucceeds(string name,
											   MailAddress email,
											   string webSiteUrl,
											   string street,
											   string city,
											   string county,
											   string postCode)
	{
		var spainId = Guid.Parse("534A826B-70EF-2128-1A4C-52E23B7D5447");
		var dto = new CompanyCreateEditDto(name,
										   email.Address,
										   webSiteUrl,
										   street,
										   city,
										   county,
										   postCode[..Address.PostCodeLength],
										   spainId);
		var response = await CreateRequest(ApiRoutes.Companies.Post()).PostJsonAsync(dto);

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.Created);

		var result = await response.GetStringAsync();

		var id = Guid.Empty;
		result.Should()
			  .Match(value => Guid.TryParse(value.Replace("\"", ""), out id));
		response.Headers
				.Should()
				.Contain(("Location", ApiRoutes.Companies.Get(id).ToString()));
		
		var companies = await GetDbContext().Set<Company>()
											.ToListAsync();
		companies.Should()
				 .HaveCount(4);

		var newCompany = companies.SingleOrDefault(c => c.Id == id);
		newCompany.Should()
				  .NotBeNull();
		newCompany!.Name
				   .Should()
				   .Be(dto.Name);
		newCompany.Email
				  .Should()
				  .Be(dto.Email);
		newCompany.WebSiteUrl
				  .Should()
				  .Be(dto.WebSiteUrl);
		newCompany.Address!.Street
				  .Should()
				  .Be(dto.Street);
		newCompany.Address!.City
				  .Should()
				  .Be(dto.City);
		newCompany.Address!.County
				  .Should()
				  .Be(dto.County);
		newCompany.Address!.PostCode
				  .Should()
				  .Be(dto.PostCode);
		newCompany.Address!.CountryId
				  .Should()
				  .Be(dto.CountryId!.Value);
	}

	[Theory(DisplayName = "Edit existing Company succeeds")]
	[AutoData]
	public async Task EditExistingCompanySucceeds(string name,
												  MailAddress email,
												  string webSiteUrl,
												  string street,
												  string city,
												  string county,
												  string postCode)
	{
		var companyId = Guid.Parse("EDEDB1E8-FD3A-4579-9EF8-A0BBEF2A6F95");
		var countryId = Guid.Parse("534A826B-70EF-2128-1A4C-52E23B7D5447");
		var dto = new CompanyCreateEditDto(name,
										   email.Address,
										   webSiteUrl,
										   street,
										   city,
										   county,
										   postCode[..Address.PostCodeLength],
										   countryId);
		var response = await CreateRequest(ApiRoutes.Companies.Put(companyId)).PutJsonAsync(dto);

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.NoContent);

		var company = await GetDbContext().Set<Company>()
										  .SingleOrDefaultAsync(c => c.Id == companyId);
		company.Should()
			   .NotBeNull();
		company!.Name
				.Should()
				.Be(dto.Name);
		company.Email
			   .Should()
			   .Be(dto.Email);
		company.WebSiteUrl
			   .Should()
			   .Be(dto.WebSiteUrl);
		company.Address!.Street
			   .Should()
			   .Be(dto.Street);
		company.Address!.City
			   .Should()
			   .Be(dto.City);
		company.Address!.County
			   .Should()
			   .Be(dto.County);
		company.Address!.PostCode
			   .Should()
			   .Be(dto.PostCode);
		company.Address!.CountryId
			   .Should()
			   .Be(dto.CountryId!.Value);
	}

	[Fact(DisplayName = "Delete existing Company succeeds")]
	public async Task DeleteExistingCompanySucceeds()
	{
		var companyId = Guid.Parse("EDEDB1E8-FD3A-4579-9EF8-A0BBEF2A6F95");
		var response = await CreateRequest(ApiRoutes.Companies.Delete(companyId)).DeleteAsync();

		response.StatusCode
				.Should()
				.Be((int)HttpStatusCode.OK);

		var companies = await GetDbContext().Set<Company>()
											.ToListAsync();
		companies.Should()
				 .HaveCount(2);
		companies.Should()
				 .NotContain(x => x.Id == companyId);
	}
}