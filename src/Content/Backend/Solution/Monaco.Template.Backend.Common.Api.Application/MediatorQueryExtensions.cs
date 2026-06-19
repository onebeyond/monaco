using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Monaco.Template.Backend.Common.Application.DTOs;
using Monaco.Template.Backend.Common.Application.Queries;
using Monaco.Template.Backend.Common.Domain.Model;

namespace Monaco.Template.Backend.Common.Api.Application;

public static class MediatorQueryExtensions
{
	extension(ISender sender)
	{
		/// <summary>
		/// Executes a query returning <see cref="QueryResult{TResult}"/> and maps it to Ok, NotFound or ValidationProblem.
		/// </summary>
		public async Task<Results<Ok<TResult>, NotFound, ValidationProblem>> ExecuteQueryAsync<TResult, TEntity>(QueryBase<TResult, TEntity> query,
																												 CancellationToken cancellationToken = default) where TEntity : class
		{
			var result = await sender.Send(query, cancellationToken);
			return result switch
			{
				Success<TResult> success => TypedResults.Ok(success.Result),
				Common.Application.Queries.NotFound<TResult> => TypedResults.NotFound(),
				ValidationFailure<TResult> f => TypedResults.ValidationProblem(f.ValidationResult.ToDictionary()),
				_ => throw new InvalidOperationException($"Unexpected query result type '{result.GetType().FullName ?? "null"}' returned for query '{query.GetType().FullName}'.")
			};
		}

		/// <summary>
		/// Executes a paged query returning <see cref="QueryResult{Page{TResult}}"/> and maps it to Ok, NotFound or ValidationProblem.
		/// </summary>
		public async Task<Results<Ok<Page<TResult>>, NotFound, ValidationProblem>> ExecutePagedQueryAsync<TResult, TEntity>(QueryPagedBase<TResult, TEntity> query,
																															CancellationToken cancellationToken = default) where TEntity : class =>
			await sender.ExecuteQueryAsync(query, cancellationToken);

		/// <summary>
		/// Executes the query passed and returns the corresponding response that can be either Ok(result), NotFound or ValidationProblem.
		/// </summary>
		/// <typeparam name="TResult">The type of the records returned by the query</typeparam>
		/// <param name="query"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<Ok<TResult>, NotFound, ValidationProblem>> ExecuteQueryAsync<TResult>(QueryBase<QueryResult<TResult>> query,
																										CancellationToken cancellationToken = default) =>
			OkOrNotFoundOrValidationProblem(await sender.Send(query, cancellationToken));

		/// <summary>
		/// Executes the paged query passed and returns the corresponding response that can be either Ok(result), NotFound or ValidationProblem.
		/// </summary>
		/// <typeparam name="TResult">The type of the records contained in the page returned by the query</typeparam>
		/// <param name="query"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<Ok<Page<TResult>>, NotFound, ValidationProblem>> ExecuteQueryAsync<TResult>(QueryPagedBase<TResult> query,
																											  CancellationToken cancellationToken = default) =>
			OkOrNotFoundOrValidationProblem(await sender.Send(query, cancellationToken));

		/// <summary>
		/// Executes the query passed and returns the corresponding response that can be either Ok(result), NotFound or ValidationProblem.
		/// </summary>
		/// <typeparam name="TResult">The type of the item returned by the query</typeparam>
		/// <param name="query"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<Ok<TResult>, NotFound, ValidationProblem>> ExecuteQueryAsync<TResult>(QueryByIdBase<QueryResult<TResult>> query,
																										CancellationToken cancellationToken = default) =>
			OkOrNotFoundOrValidationProblem(await sender.Send(query, cancellationToken));

		/// <summary>
		/// Executes the query passed and returns the corresponding response that can be either Ok(result), NotFound or ValidationProblem.
		/// </summary>
		/// <typeparam name="TResult">The type of the item returned by the query</typeparam>
		/// <typeparam name="TKey">The type of the key to search the item by</typeparam>
		/// <param name="query"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<Ok<TResult>, NotFound, ValidationProblem>> ExecuteQueryAsync<TResult, TKey>(QueryByKeyBase<QueryResult<TResult>, TKey> query,
																											  CancellationToken cancellationToken = default) =>
			OkOrNotFoundOrValidationProblem(await sender.Send(query, cancellationToken));

		/// <summary>
		/// Executes the query passed and returns a FileStreamResult, NotFound or ValidationProblem.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="query"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<FileStreamHttpResult, NotFound, ValidationProblem>> ExecuteFileDownloadAsync<TResult>(QueryBase<QueryResult<TResult>> query,
																														CancellationToken cancellationToken = default) where TResult : FileDownloadDto =>
			FileDownloadOrNotFoundOrValidationProblem(await sender.Send(query, cancellationToken));

		/// <summary>
		/// Executes the query passed and returns a FileStreamResult, NotFound or ValidationProblem.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="query"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<Results<FileStreamHttpResult, NotFound, ValidationProblem>> ExecuteFileDownloadAsync<TResult>(QueryByIdBase<QueryResult<TResult>> query,
																														CancellationToken cancellationToken = default) where TResult : FileDownloadDto =>
			FileDownloadOrNotFoundOrValidationProblem(await sender.Send(query, cancellationToken));
	}

	private static Results<FileStreamHttpResult, NotFound, ValidationProblem> FileDownloadOrNotFoundOrValidationProblem<TResult>(QueryResult<TResult> result) where TResult : FileDownloadDto =>
		ResponseOrNotFoundOrValidationProblem(result,
											  success => TypedResults.File(success.FileContent,
																		   success.ContentType,
																		   success.FileName));

	private static Results<Ok<TResult>, NotFound, ValidationProblem> OkOrNotFoundOrValidationProblem<TResult>(QueryResult<TResult> result) =>
		ResponseOrNotFoundOrValidationProblem(result, TypedResults.Ok);

	private static Results<TResponse, NotFound, ValidationProblem> ResponseOrNotFoundOrValidationProblem<TResult, TResponse>(QueryResult<TResult> result, Func<TResult, TResponse> func) where TResponse : IResult =>
		result switch
		{
			Success<TResult> success => func(success.Result),
			Common.Application.Queries.NotFound<TResult> => TypedResults.NotFound(),
			ValidationFailure<TResult> validationFailed => TypedResults.ValidationProblem(validationFailed.ValidationResult.ToDictionary()),
			_ => throw new InvalidOperationException($"Unexpected query result type '{result.GetType().FullName ?? "null"}' returned.")
		};

}