using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Monaco.Template.Backend.Api.DTOs;
using Monaco.Template.Backend.Api.DTOs.Extensions;
using Monaco.Template.Backend.Application.Features.Company;
using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Api.MinimalApi;
using Monaco.Template.Backend.Common.Domain.Model;
#if (auth)
using Monaco.Template.Backend.Api.Auth;
#endif

namespace Monaco.Template.Backend.Api.Endpoints;

internal static class Companies
{
	extension(IEndpointRouteBuilder builder)
	{
		public IEndpointRouteBuilder AddCompanies(ApiVersionSet versionSet)
		{
			var companies = builder.CreateApiGroupBuilder(versionSet,
														  "Companies");

			companies.MapGet("",
							 Task<Results<Ok<Page<CompanyDto>>, NotFound>> ([FromServices] ISender sender,
																			HttpRequest request,
																			CancellationToken cancellationToken) =>
								 sender.ExecuteQueryAsync(new GetCompanyPage.Query(request.Query),
														  cancellationToken),
							 "GetCompanies",
#if (!auth)
							 "Gets a page of companies");
#else
							 "Gets a page of companies")
					 .RequireAuthorization(Scopes.CompaniesRead);
#endif

			companies.MapGet("{id:guid}",
							 Task<Results<Ok<CompanyDto?>, NotFound>> ([FromServices] ISender sender,
																	   [FromRoute] Guid id,
																	   CancellationToken cancellationToken) =>
								 sender.ExecuteQueryAsync(new GetCompanyById.Query(id),
														  cancellationToken),
							 "GetCompany",
#if (!auth)
							 "Gets a company by Id");
#else
							 "Gets a company by Id")
					 .RequireAuthorization(Scopes.CompaniesRead);
#endif

			companies.MapPost("",
							  Task<Results<Created<CreatedResponse>, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ([FromServices] ISender sender,
																																[FromBody] CompanyCreateEditDto dto,
																																HttpContext context,
																																CancellationToken cancellationToken) =>
								  sender.ExecuteCommandCreatedAsync(dto.MapCreateCommand(),
																	"api/v{0}/Companies/{1}",
																	[context.GetRequestedApiVersion()!],
																	cancellationToken),
							  "CreateCompany",
#if (!auth)
							  "Create a new company");
#else
							  "Create a new company")
					 .RequireAuthorization(Scopes.CompaniesWrite);
#endif

			companies.MapPut("{id:guid}",
							 Task<Results<NoContent, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ([FromServices] ISender sender,
																												[FromRoute] Guid id,
																												[FromBody] CompanyCreateEditDto dto,
																												CancellationToken cancellationToken) =>
								 sender.ExecuteCommandNoContentAsync(dto.MapEditCommand(id),
																	 cancellationToken),
							 "EditCompany",
#if (!auth)
							 "Edit an existing company by Id");
#else
							 "Edit an existing company by Id")
					 .RequireAuthorization(Scopes.CompaniesWrite);
#endif

			companies.MapDelete("{id:guid}",
								Task<Results<Ok, NotFound, ValidationProblem, Conflict, ForbidHttpResult>> ([FromServices] ISender sender,
																											[FromRoute] Guid id,
																											CancellationToken cancellationToken) =>
									sender.ExecuteCommandOkAsync(new DeleteCompany.Command(id),
																 cancellationToken),
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
}