using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Application.Commands.Contracts;
using Monaco.Template.Backend.Common.Infrastructure.Context;
using Polly;
using Polly.Registry;

namespace Monaco.Template.Backend.Common.Application.Commands.Behaviors;

public class ConcurrencyExceptionBehavior<TCommand> : IPipelineBehavior<TCommand, ICommandResult>
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

	public Task<ICommandResult> Handle(TCommand request,
									   RequestHandlerDelegate<ICommandResult> next,
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

public class ConcurrencyExceptionBehavior<TCommand, TResult> : IPipelineBehavior<TCommand, ICommandResult<TResult?>>
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

	public Task<ICommandResult<TResult?>> Handle(TCommand request,
												 RequestHandlerDelegate<ICommandResult<TResult?>> next,
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