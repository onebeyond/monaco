using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Application.DTOs;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Common.Api.Application;

public static class MediatorExtensions
{
	/// <summary>
	/// Executes the query passed and returns the corresponding response that can be either Ok(result) or a NotFound() result depending on whether the retuned result is null or not
	/// </summary>
	/// <typeparam name="TResult">The type of the records returned by the query</typeparam>
	/// <param name="sender"></param>
	/// <param name="query"></param>
	/// <returns></returns>
	public static async Task<Results<Ok<TResult>, NotFound>> ExecuteQueryAsync<TResult>(this ISender sender,
																						QueryBase<TResult> query)
	{
		var result = await sender.Send(query);
		return result is null
				   ? TypedResults.NotFound()
				   : TypedResults.Ok(result);
	}

	/// <summary>
	/// Executes the paged query passed and returns the corresponding response that can be either Ok(result) or a NotFound() result depending on whether the returned result is null or not
	/// </summary>
	/// <typeparam name="TResult">The type of the records contained in the page returned by the query</typeparam>
	/// <param name="sender"></param>
	/// <param name="query"></param>
	/// <returns></returns>
	public static async Task<Results<Ok<Page<TResult>>, NotFound>> ExecuteQueryAsync<TResult>(this ISender sender,
																							  QueryPagedBase<TResult> query)
	{
		var result = await sender.Send(query);
		return result is null
				   ? TypedResults.NotFound()
				   : TypedResults.Ok(result);
	}

	/// <summary>
	/// Executes the query passed and returns the corresponding response that can be either Ok(result) or a NotFound() result depending on whether the returned item is null or not
	/// </summary>
	/// <typeparam name="TResult">The type of the item returned by the query</typeparam>
	/// <param name="sender"></param>
	/// <param name="query"></param>
	/// <returns></returns>
	public static async Task<Results<Ok<TResult>, NotFound>> ExecuteQueryAsync<TResult>(this ISender sender,
																						QueryByIdBase<TResult> query)
	{
		var result = await sender.Send(query);
		return result is null
				   ? TypedResults.NotFound()
				   : TypedResults.Ok(result);
	}

	/// <summary>
	/// Executes the query passed and returns the corresponding response that can be either Ok(result) or a NotFound() result depending on whether the returned item is null or not
	/// </summary>
	/// <typeparam name="TResult">The type of the item returned by the query</typeparam>
	/// <typeparam name="TKey">The type of the key to search the item by</typeparam>
	/// <param name="sender"></param>
	/// <param name="query"></param>
	/// <returns></returns>
	public static async Task<Results<Ok<TResult>, NotFound>> ExecuteQueryAsync<TResult, TKey>(this ISender sender,
																							  QueryByKeyBase<TResult, TKey> query)
	{
		var item = await sender.Send(query);
		return item is null
				   ? TypedResults.NotFound()
				   : TypedResults.Ok(item);
	}

	/// <summary>
	/// Executes the query passed and returns a FileStreamResult for allowing download of a file or a NotFound() result depending on whether the returned item is null or not
	/// </summary>
	/// <typeparam name="TResult"></typeparam>
	/// <param name="sender"></param>
	/// <param name="query"></param>
	/// <returns></returns>
	public static async Task<Results<FileStreamHttpResult, NotFound>> ExecuteFileDownloadAsync<TResult>(this ISender sender,
																										QueryBase<TResult?> query) where TResult : FileDownloadDto
	{
		var item = await sender.Send(query);
		return GetFileDownload(item);
	}

	/// <summary>
	/// Executes the query passed and returns a FileStreamResult for allowing download of a file or a NotFound() result depending on whether the returned item is null or not
	/// </summary>
	/// <typeparam name="TResult"></typeparam>
	/// <param name="sender"></param>
	/// <param name="query"></param>
	/// <returns></returns>
	public static async Task<Results<FileStreamHttpResult, NotFound>> ExecuteFileDownloadAsync<TResult>(this ISender sender,
																										QueryByIdBase<TResult?> query) where TResult : FileDownloadDto
	{
		var item = await sender.Send(query);
		return GetFileDownload(item);
	}

	private static Results<FileStreamHttpResult, NotFound> GetFileDownload<TResult>(TResult? item) where TResult : FileDownloadDto =>
		item is null
			? TypedResults.NotFound()
			: TypedResults.File(item.FileContent, item.ContentType, item.FileName);

	/// <summary>
	/// Executes the command passed and returns the corresponding response that can be either Created(result) or a NotFound() or a ValidationProblem() depending on the validations and processing
	/// </summary>
	/// <typeparam name="TResult">The type of the result returned by the command</typeparam>
	/// <param name="sender"></param>
	/// <param name="command"></param>
	/// <param name="resultUri">The URI to include in the headers of the Created() response</param>
	/// <param name="uriParams">The parameters (if any) to pass for concatenating into the resultUri</param>
	/// <returns></returns>
	public static async Task<Results<Created<TResult>, NotFound, ValidationProblem>> ExecuteCommandAsync<TResult>(this ISender sender,
																												  CommandBase<TResult> command,
																												  string resultUri,
																												  params object[]? uriParams)
	{
		var result = await sender.Send(command);
		return result switch
			   {
				   { ItemNotFound: true } => TypedResults.NotFound(),
				   { ValidationResult.IsValid: false } => TypedResults.ValidationProblem(result.ValidationResult.ToDictionary()),
				   _ => TypedResults.Created(string.Format(resultUri,
														   [.. uriParams ?? [], result.Result!]),
											 result.Result)
			   };
	}

	/// <summary>
	/// Executes the edit command passed and returns the corresponding response that can be either NoContent() or a NotFound() or a ValidationProblem() depending on the validations and processing
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="command"></param>
	/// <returns></returns>
	public static async Task<Results<NoContent, NotFound, ValidationProblem>> ExecuteCommandEditAsync(this ISender sender,
																									  CommandBase command)
	{
		var result = await sender.Send(command);
		return result switch
			   {
				   { ItemNotFound: true } => TypedResults.NotFound(),
				   { ValidationResult.IsValid: false } => TypedResults.ValidationProblem(result.ValidationResult.ToDictionary()),
				   _ => TypedResults.NoContent()
			   };
	}

	/// <summary>
	/// Executes the delete command passed and returns the corresponding response that can be either Ok() or a NotFound() or a ValidationProblem() depending on the validations and processing
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="command"></param>
	/// <returns></returns>
	public static async Task<Results<Ok, NotFound, ValidationProblem>> ExecuteCommandDeleteAsync(this ISender sender,
																								 CommandBase command)
	{
		var result = await sender.Send(command);
		return result switch
			   {
				   { ItemNotFound: true } => TypedResults.NotFound(),
				   { ValidationResult.IsValid: false } => TypedResults.ValidationProblem(result.ValidationResult.ToDictionary()),
				   _ => TypedResults.Ok()
			   };
	}
}