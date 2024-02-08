using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
#if (auth)
using Monaco.Template.Backend.Api.Auth;
#endif
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Api.DTOs.Extensions;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Api.MinimalApi;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Api.Endpoints;

public static class Companies
{
	public static IEndpointRouteBuilder AddCompanies(this IEndpointRouteBuilder builder, ApiVersionSet versionSet)
	{
		var companies = builder.CreateApiGroupBuilder(versionSet, "Companies");

		companies.MapGet("",
						 Task<Results<Ok<Page<CompanyDto>>, NotFound>> ([FromServices] ISender sender,
																		HttpRequest request) =>
							 sender.ExecuteQueryAsync(new GetCompanyPage.Query(request.Query)),
						 "GetCompanies",
#if (!auth)
						 "Gets a page of companies");
#else
						 "Gets a page of companies")
				 .RequireAuthorization(Scopes.CompaniesRead);
#endif

		companies.MapGet("{id:guid}",
						 Task<Results<Ok<CompanyDto?>, NotFound>> ([FromServices] ISender sender,
																   [FromRoute] Guid id) =>
							 sender.ExecuteQueryAsync(new GetCompanyById.Query(id)),
						 "GetCompany",
#if (!auth)
						 "Gets a company by Id");
#else
						 "Gets a company by Id")
				 .RequireAuthorization(Scopes.CompaniesRead);
#endif

		companies.MapPost("",
						  Task<Results<Created<Guid>, NotFound, ValidationProblem>> ([FromServices] ISender sender,
																					 [FromBody] CompanyCreateEditDto dto,
																					 HttpContext context) =>
							  sender.ExecuteCommandAsync(dto.MapCreateCommand(), "api/v{0}/Companies/{1}", context.GetRequestedApiVersion()!),
						  "CreateCompany",
#if (!auth)
						  "Create a new company");
#else
						  "Create a new company")
				 .RequireAuthorization(Scopes.CompaniesWrite);
#endif

		companies.MapPut("{id:guid}",
						 Task<Results<NoContent, NotFound, ValidationProblem>> ([FromServices] ISender sender,
																				[FromRoute] Guid id,
																				[FromBody] CompanyCreateEditDto dto) =>
							 sender.ExecuteCommandEditAsync(dto.MapEditCommand(id)),
						 "EditCompany",
#if (!auth)
						 "Edit an existing company by Id");
#else
						 "Edit an existing company by Id")
				 .RequireAuthorization(Scopes.CompaniesWrite);
#endif

		companies.MapDelete("{id:guid}",
							Task<Results<Ok, NotFound, ValidationProblem>> ([FromServices] ISender sender,
																			[FromRoute] Guid id) =>
								sender.ExecuteCommandDeleteAsync(new DeleteCompany.Command(id)),
							"DeleteCompany",
#if (!auth)
							"Delete an existing company by Id");
#else
							"Delete an existing company by Id")
				 .RequireAuthorization(Scopes.CompaniesWrite);
#endif

		return builder;
	}
}