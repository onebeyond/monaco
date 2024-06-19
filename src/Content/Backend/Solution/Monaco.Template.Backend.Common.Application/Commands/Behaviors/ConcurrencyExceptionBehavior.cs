using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using Polly;
using Polly.Registry;

namespace Monaco.Template.Backend.Common.Application.Commands.Behaviors;

public class ConcurrencyExceptionBehavior<TCommand> : IPipelineBehavior<TCommand, CommandResult>
	where TCommand : CommandBase
{
	private readonly IAsyncPolicy _dbConcurrentRetryPolicy;
	private readonly BaseDbContext _dbContext;

	public ConcurrencyExceptionBehavior(IReadOnlyPolicyRegistry<string> policyRegistry,
										BaseDbContext dbContext)
	{
		_dbConcurrentRetryPolicy = policyRegistry.Get<IAsyncPolicy>(Policies.Policies.DbConcurrentExceptionPolicyKey);
		_dbContext = dbContext;
	}

	public Task<CommandResult> Handle(TCommand request,
									  RequestHandlerDelegate<CommandResult> next,
									  CancellationToken cancellationToken) =>
		_dbConcurrentRetryPolicy.ExecuteAsync(async () =>
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
											  });
}

public class ConcurrencyExceptionBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, CommandResult<TResult?>>
	where TCommand : CommandBase<TResult?>
{
	private readonly IAsyncPolicy _dbConcurrentRetryPolicy;
	private readonly BaseDbContext _dbContext;

	public ConcurrencyExceptionBehavior(IReadOnlyPolicyRegistry<string> policyRegistry,
										BaseDbContext dbContext)
	{
		_dbConcurrentRetryPolicy = policyRegistry.Get<IAsyncPolicy>(Policies.Policies.DbConcurrentExceptionPolicyKey);
		_dbContext = dbContext;
	}

	public Task<CommandResult<TResult?>> Handle(TCommand request,
												RequestHandlerDelegate<CommandResult<TResult?>> next,
												CancellationToken cancellationToken) =>
		_dbConcurrentRetryPolicy.ExecuteAsync(() =>
											  {
												  try
												  {
													  return next();
												  }
												  catch (DbUpdateConcurrencyException)
												  {
													  _dbContext.ChangeTracker.Clear();
													  throw;
												  }
											  });
}