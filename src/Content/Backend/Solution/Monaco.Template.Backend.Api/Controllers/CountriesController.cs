using MediatR;
using Microsoft.AspNetCore.Mvc;
using Monaco.Template.Backend.Application.DTOs;
using Monaco.Template.Backend.Application.Features.Country;
using Monaco.Template.Backend.Common.Api.Application;

namespace Monaco.Template.Backend.Api.Controllers;

[Route("api/v{apiVersion:apiVersion}/[controller]")]
[ApiController]
public class CountriesController : ControllerBase
{
	private readonly IMediator _mediator;

	public CountriesController(IMediator mediator)
	{
		_mediator = mediator;
	}

	/// <summary>
	/// Gets a list of countries
	/// </summary>
	/// <returns></returns>
	[HttpGet]
	public Task<ActionResult<List<CountryDto>>> Get() =>
		_mediator.ExecuteQueryAsync(new GetCountryList.Query(Request.Query));

	/// <summary>
	/// Gets a country by Id
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	[HttpGet("{id:guid}")]
	public Task<ActionResult<CountryDto?>> Get(Guid id) =>
		_mediator.ExecuteQueryAsync(new GetCountryById.Query(id));
}