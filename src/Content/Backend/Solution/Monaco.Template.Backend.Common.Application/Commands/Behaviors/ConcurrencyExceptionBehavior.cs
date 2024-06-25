using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Application.ResiliencePipelines;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using Polly;
using Polly.Registry;

namespace Monaco.Template.Backend.Common.Application.Commands.Behaviors;

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