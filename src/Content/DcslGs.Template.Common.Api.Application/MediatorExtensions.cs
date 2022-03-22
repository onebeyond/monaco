using DcslGs.Template.Common.Api.Application.Enums;
using DcslGs.Template.Common.Application.Commands;
using DcslGs.Template.Common.Application.Queries;
using DcslGs.Template.Common.Domain.Model;
using FluentValidation.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DcslGs.Template.Common.Api.Application;

public static class MediatorExtensions
{
	/// <summary>
	/// Executes the query passed and returns the corresponding response that can be either Ok(result) or a NotFound() result depending on whether the retuned result is null or not
	/// </summary>
	/// <typeparam name="TResult">The type of the records returned by the query</typeparam>
	/// <param name="mediator"></param>
	/// <param name="query"></param>
	/// <returns></returns>
	public static async Task<ActionResult<TResult>> ExecuteQueryAsync<TResult>(this IMediator mediator,
																			   QueryBase<TResult> query)
	{
		var result = await mediator.Send(query);

		if (result == null)
			return new NotFoundResult();

		return new OkObjectResult(result);
	}

	/// <summary>
	/// Executes the paged query passed and returns the corresponding response that can be either Ok(result) or a NotFound() result depending on whether the returned result is null or not
	/// </summary>
	/// <typeparam name="TResult">The type of the records contained in the page returned by the query</typeparam>
	/// <param name="mediator"></param>
	/// <param name="query"></param>
	/// <returns></returns>
	public static async Task<ActionResult<Page<TResult>>> ExecuteQueryAsync<TResult>(this IMediator mediator,
																					 QueryPagedBase<TResult> query)
	{
		var result = await mediator.Send(query);

		if (result == null)
			return new NotFoundResult();

		return new OkObjectResult(result);
	}

	/// <summary>
	/// Executes the query passed and returns the corresponding response that can be either Ok(result) or a NotFound() result depending on whether the returned item is null or not
	/// </summary>
	/// <typeparam name="TResult">The type of the item returned by the query</typeparam>
	/// <param name="mediator"></param>
	/// <param name="query"></param>
	/// <returns></returns>
	public static async Task<ActionResult<TResult>> ExecuteQueryAsync<TResult>(this IMediator mediator,
																			   QueryByIdBase<TResult> query)
	{
		var item = await mediator.Send(query);

		if (item == null)
			return new NotFoundResult();

		return new OkObjectResult(item);
	}

	/// <summary>
	/// Executes the command passed and returns the corresponding response that can be either Created(result) or a NotFound() or a BadRequest() depending on the validations and processing
	/// </summary>
	/// <typeparam name="TResult">The type of the result returned by the command</typeparam>
	/// <param name="mediator"></param>
	/// <param name="command"></param>
	/// <param name="modelState">ModelState of the controller to return the Validation errors</param>
	/// <param name="resultUri">The URI to include in the headers of the Created() response</param>
	/// <param name="uriParams">The parameters (if any) to pass for concatenating into the resultUri</param>
	/// <returns></returns>
	public static async Task<ActionResult<TResult>> ExecuteCommandAsync<TResult>(this IMediator mediator,
																				 CommandBase<TResult> command,
																				 ModelStateDictionary modelState,
																				 string resultUri,
																				 params object[]? uriParams)
	{
		var result = await mediator.Send(command);

		if (result.ItemNotFound)
			return new NotFoundResult();

		if (result.ValidationResult.IsValid)
		{
			var parameters = (uriParams ?? Array.Empty<object>()).Append(result.Result!);
			return new CreatedResult(string.Format(resultUri, parameters.ToArray()), result.Result);
		}

		result.ValidationResult.AddToModelState(modelState, null);
		return new BadRequestObjectResult(modelState);
	}

	/// <summary>
	/// Executes the command passed and returns the corresponding response that can be either Ok() or a NotFound() or a BadRequest() depending on the validations and processing
	/// </summary>
	/// <param name="mediator"></param>
	/// <param name="command"></param>
	/// <param name="modelState">ModelState of the controller to return the Validation errors</param>
	/// <param name="responseType">An enum option to select if the status returned in the response is Ok() or NoResult() (defaults to Ok)</param>
	/// <returns></returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static async Task<IActionResult> ExecuteCommandAsync(this IMediator mediator,
																CommandBase command,
																ModelStateDictionary modelState,
																ResponseType responseType = ResponseType.Ok)
	{
		var result = await mediator.Send(command);

		if (result.ItemNotFound)
			return new NotFoundResult();

		if (result.ValidationResult.IsValid)
			return responseType switch
			{
				ResponseType.Ok => new OkResult(),
				ResponseType.NoContent => new NoContentResult(),
				_ => throw new ArgumentOutOfRangeException(nameof(responseType), responseType, null)
			};

		result.ValidationResult.AddToModelState(modelState, null);
		return new BadRequestObjectResult(modelState);
	}
}