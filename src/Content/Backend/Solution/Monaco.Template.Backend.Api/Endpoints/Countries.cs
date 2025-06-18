using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Monaco.Template.Backend.Application.Features.Country;
using Monaco.Template.Backend.Application.Features.Country.DTOs;
using Monaco.Template.Backend.Common.Api.Application;
using Monaco.Template.Backend.Common.Api.MinimalApi;

namespace Monaco.Template.Backend.Api.Endpoints;

internal static class Countries
{
	public static IEndpointRouteBuilder AddCountries(this IEndpointRouteBuilder builder, ApiVersionSet versionSet)
	{
		var countries = builder.CreateApiGroupBuilder(versionSet, "Countries");

		countries.MapGet("",
						 Task<Results<Ok<List<CountryDto>>, NotFound>> ([FromServices] ISender sender,
																		 HttpRequest request) =>
							 sender.ExecuteQueryAsync(new GetCountryList.Query(request.Query)),
						 "GetCountries",
						 "Gets a list of countries");

		countries.MapGet("{id:guid}",
						 Task<Results<Ok<CountryDto?>, NotFound>> ([FromServices] ISender sender,
																   [FromRoute] Guid id) =>
							 sender.ExecuteQueryAsync(new GetCountryById.Query(id)),
						 "GetCountry",
						 "Gets a country by Id");

		return builder;
	}
}