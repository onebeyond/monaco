using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;
using Monaco.Template.Backend.Common.Application.ResiliencePipelines;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using Polly;
using Polly.Registry;

namespace Monaco.Template.Backend.Common.Application.Commands.Behaviors;

/// <summary>
/// Implements a pipeline behavior that handles <see cref="DbUpdateConcurrencyException"/> during the execution
/// of a command that returns a <see cref="CommandResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// When the command implements <see cref="IConcurrencyRetriable"/>, a resilience pipeline retries the operation
/// up to the configured maximum attempts. On each <see cref="DbUpdateConcurrencyException"/>, the change tracker
/// of the associated <see cref="BaseDbContext"/> is cleared to reset tracked entity states before the retry.
/// If all retries are exhausted, a <see cref="CommandResult"/> with a concurrency-conflicted status is returned.
/// </para>
/// <para>
/// When the command does not implement <see cref="IConcurrencyRetriable"/>, no retry is attempted. Instead,
/// the exception is caught and a <see cref="CommandResult"/> with a concurrency-conflicted status is returned
/// immediately, allowing the caller to handle the conflict explicitly.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command being processed. Must implement <see cref="IRequest{CommandResult}"/>.</typeparam>
public class ConcurrencyExceptionBehavior<TCommand> : IPipelineBehavior<TCommand, CommandResult>
	where TCommand : IRequest<CommandResult>
{
	private readonly ResiliencePipeline _dbConcurrentRetryPipeline;
	private readonly BaseDbContext _dbContext;

	public ConcurrencyExceptionBehavior(ResiliencePipelineProvider<string> pipelineProvider,
										BaseDbContext dbContext)
	{
		_dbConcurrentRetryPipeline = pipelineProvider.GetPipeline(ResiliencePipelinesExtensions.DbConcurrentExceptionPipelineKey);
		_dbContext = dbContext;
	}

	public async Task<CommandResult> Handle(TCommand request,
											RequestHandlerDelegate<CommandResult> next,
											CancellationToken cancellationToken)
	{
		if (request is IConcurrencyRetriable)
			try
			{
				return await _dbConcurrentRetryPipeline.ExecuteAsync(async ct =>
				{
					try
					{
						return await next(ct);
					}
					catch (DbUpdateConcurrencyException)
					{
						_dbContext.ChangeTracker.Clear();
						throw;
					}
				},
																	 cancellationToken);
			}
			catch (DbUpdateConcurrencyException)
			{
				return CommandResult.ConcurrencyConflict();
			}

		try
		{
			return await next(cancellationToken);
		}
		catch (DbUpdateConcurrencyException)
		{
			return CommandResult.ConcurrencyConflict();
		}
	}
}

/// <summary>
/// Implements a pipeline behavior that handles <see cref="DbUpdateConcurrencyException"/> during the execution
/// of a command that returns a <see cref="CommandResult{TResult}"/>.
/// </summary>
/// <remarks>
/// <para>
/// When the command implements <see cref="IConcurrencyRetriable"/>, a resilience pipeline retries the operation
/// up to the configured maximum attempts. On each <see cref="DbUpdateConcurrencyException"/>, the change tracker
/// of the associated <see cref="BaseDbContext"/> is cleared to reset tracked entity states before the retry.
/// If all retries are exhausted, a <see cref="CommandResult{TResult}"/> with a concurrency-conflicted status
/// and a default <typeparamref name="TResult"/> value is returned.
/// </para>
/// <para>
/// When the command does not implement <see cref="IConcurrencyRetriable"/>, no retry is attempted. Instead,
/// the exception is caught and a <see cref="CommandResult{TResult}"/> with a concurrency-conflicted status
/// and a default <typeparamref name="TResult"/> value is returned immediately, allowing the caller to handle
/// the conflict explicitly.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The type of the command being processed. Must implement <see cref="IRequest{CommandResult{TResult?}}"/>.</typeparam>
/// <typeparam name="TResult">The type of the result value produced by the command.</typeparam>
public class ConcurrencyExceptionBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, CommandResult<TResult?>>
	where TCommand : IRequest<CommandResult<TResult?>>
{
	private readonly ResiliencePipeline _dbConcurrentRetryPipeline;
	private readonly BaseDbContext _dbContext;

	public ConcurrencyExceptionBehavior(ResiliencePipelineProvider<string> pipelineProvider,
										BaseDbContext dbContext)
	{
		_dbConcurrentRetryPipeline = pipelineProvider.GetPipeline(ResiliencePipelinesExtensions.DbConcurrentExceptionPipelineKey);
		_dbContext = dbContext;
	}

	public async Task<CommandResult<TResult?>> Handle(TCommand request,
													  RequestHandlerDelegate<CommandResult<TResult?>> next,
													  CancellationToken cancellationToken)
	{
		if (request is IConcurrencyRetriable)
			try
			{
				return await _dbConcurrentRetryPipeline.ExecuteAsync(async ct =>
				{
					try
					{
						return await next(ct);
					}
					catch (DbUpdateConcurrencyException)
					{
						_dbContext.ChangeTracker.Clear();
						throw;
					}
				},
																	 cancellationToken);
			}
			catch (DbUpdateConcurrencyException)
			{
				return CommandResult<TResult?>.ConcurrencyConflict();
			}


		try
		{
			return await next(cancellationToken);
		}
		catch (DbUpdateConcurrencyException)
		{
			return CommandResult<TResult?>.ConcurrencyConflict();
		}
	}
}