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
	public static async Task<ActionResult<TResult>> ExecuteQueryAsync<TResult>(this IMediator mediator,
																			   QueryBase<TResult> query)
	{
		var result = await mediator.Send(query);
		return new OkObjectResult(result);
	}
	
	public static async Task<ActionResult<Page<TResult>>> ExecuteQueryAsync<TResult>(this IMediator mediator,
																					 QueryPagedBase<TResult> query)
	{
		var result = await mediator.Send(query);
		return new OkObjectResult(result);
	}

	public static async Task<ActionResult<TResult>> ExecuteQueryAsync<TResult>(this IMediator mediator,
																			   QueryByIdBase<TResult> query)
	{
		var item = await mediator.Send(query);

		if (item == null)
			return new NotFoundResult();

		return new OkObjectResult(item);
	}

	public static async Task<ActionResult<TResult>> ExecuteCommandAsync<TResult>(this IMediator mediator,
																				 CommandBase<TResult> command,
																				 ModelStateDictionary modelState,
																				 string resultUri)
	{
		var result = await mediator.Send(command);

		if (result.ValidationResult.IsValid)
			return new CreatedResult(string.Format(resultUri, result), result);

		result.ValidationResult.AddToModelState(modelState, null);
		return new BadRequestObjectResult(modelState);
	}

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