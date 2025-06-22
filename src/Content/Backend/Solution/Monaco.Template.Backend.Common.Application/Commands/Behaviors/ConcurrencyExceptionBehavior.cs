using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Application.ResiliencePipelines;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using Polly;
using Polly.Registry;

namespace Monaco.Template.Backend.Common.Application.Commands.Behaviors;

/// <summary>
/// Implements a pipeline behavior that handles database concurrency exceptions during the execution of a command.
/// </summary>
/// <remarks>This behavior uses a resilience pipeline to retry operations that encounter database concurrency
/// exceptions. If a <see cref="DbUpdateConcurrencyException"/> is thrown, the behavior clears the change tracker of the
/// associated <see cref="BaseDbContext"/> to reset the tracked entity states before rethrowing the exception.</remarks>
/// <typeparam name="TCommand">The type of the command being processed. Must inherit from <see cref="CommandBase"/>.</typeparam>
public class ConcurrencyExceptionBehavior<TCommand> : IPipelineBehavior<TCommand, CommandResult>
	where TCommand : CommandBase
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
											CancellationToken cancellationToken) =>
		await _dbConcurrentRetryPipeline.ExecuteAsync(async _ =>
													  {
														  try
														  {
															  return await next();
														  }
														  catch (DbUpdateConcurrencyException)
														  {
															  _dbContext.ChangeTracker.Clear();
															  throw;
														  }
													  },
													  cancellationToken);
}

/// <summary>
/// Implements a pipeline behavior that handles database concurrency exceptions during the execution of a command.
/// </summary>
/// <remarks>This behavior uses a resilience pipeline to retry operations that encounter database concurrency
/// exceptions. If a <see cref="DbUpdateConcurrencyException"/> is thrown, the behavior clears the change tracker of the
/// associated <see cref="BaseDbContext"/> to reset the tracked entity states before rethrowing the exception.</remarks>
/// <typeparam name="TCommand">The type of the command being processed. Must inherit from <see cref="CommandBase{TResult}"/>.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the command.</typeparam>
public class ConcurrencyExceptionBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, CommandResult<TResult?>>
	where TCommand : CommandBase<TResult?>
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
													  CancellationToken cancellationToken) =>
		await _dbConcurrentRetryPipeline.ExecuteAsync(async _ =>
													  {
														  try
														  {
															  return await next();
														  }
														  catch (DbUpdateConcurrencyException)
														  {
															  _dbContext.ChangeTracker.Clear();
															  throw;
														  }
													  },
													  cancellationToken);
}