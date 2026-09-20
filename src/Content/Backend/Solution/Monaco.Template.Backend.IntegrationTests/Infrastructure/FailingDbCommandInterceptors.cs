using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Monaco.Template.Backend.IntegrationTests.Infrastructure;

[ExcludeFromCodeCoverage]
internal sealed class CompanyInsertRaiserrorInterceptor : DbCommandInterceptor
{
	public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
																					 CommandEventData eventData,
																					 InterceptionResult<DbDataReader> result,
																					 CancellationToken cancellationToken = default)
	{
		// RAISERROR keeps a native SqlClient error span; a client throw would skip that activity.
		if (command.CommandText.Contains("INSERT INTO [Company]", StringComparison.Ordinal))
			command.CommandText = "RAISERROR ('test persistence fault', 16, 1);";

		return ValueTask.FromResult(result);
	}
}

[ExcludeFromCodeCoverage]
internal sealed class ThrowOnWriteDbCommandInterceptor : DbCommandInterceptor
{
	public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
																	 CommandEventData eventData,
																	 InterceptionResult<DbDataReader> result)
	{
		FailIfWriting(command);
		return result;
	}

	public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
																					 CommandEventData eventData,
																					 InterceptionResult<DbDataReader> result,
																					 CancellationToken cancellationToken = default)
	{
		FailIfWriting(command);
		return ValueTask.FromResult(result);
	}

	public override InterceptionResult<int> NonQueryExecuting(DbCommand command,
															  CommandEventData eventData,
															  InterceptionResult<int> result)
	{
		FailIfWriting(command);
		return result;
	}

	public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
																			  CommandEventData eventData,
																			  InterceptionResult<int> result,
																			  CancellationToken cancellationToken = default)
	{
		FailIfWriting(command);
		return ValueTask.FromResult(result);
	}

	private static void FailIfWriting(DbCommand command)
	{
		if (IsWrite(command))
			throw new InvalidOperationException("Forced persistence failure.");
	}

	private static bool IsWrite(DbCommand command)
	{
		var text = command.CommandText;
		return text.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
			   text.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
			   text.Contains("DELETE", StringComparison.OrdinalIgnoreCase);
	}
}

[ExcludeFromCodeCoverage]
internal sealed class TransientOnceDbCommandInterceptor : DbCommandInterceptor
{
	private int _failures;

	public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
																	 CommandEventData eventData,
																	 InterceptionResult<DbDataReader> result)
	{
		FailOnceIfWriting(command);
		return result;
	}

	public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
																					 CommandEventData eventData,
																					 InterceptionResult<DbDataReader> result,
																					 CancellationToken cancellationToken = default)
	{
		FailOnceIfWriting(command);
		return ValueTask.FromResult(result);
	}

	public override InterceptionResult<int> NonQueryExecuting(DbCommand command,
															  CommandEventData eventData,
															  InterceptionResult<int> result)
	{
		FailOnceIfWriting(command);
		return result;
	}

	public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
																			  CommandEventData eventData,
																			  InterceptionResult<int> result,
																			  CancellationToken cancellationToken = default)
	{
		FailOnceIfWriting(command);
		return ValueTask.FromResult(result);
	}

	private void FailOnceIfWriting(DbCommand command)
	{
		if (!IsWrite(command) || Interlocked.Increment(ref _failures) != 1)
			return;

		throw new TimeoutException("Transient test failure.");
	}

	private static bool IsWrite(DbCommand command)
	{
		var text = command.CommandText;
		return text.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
			   text.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
			   text.Contains("DELETE", StringComparison.OrdinalIgnoreCase);
	}
}