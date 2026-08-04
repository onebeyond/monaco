using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Monaco.Template.Backend.Common.Observability;

public static class BoundaryExceptionDiagnostics
{
	internal const string Category = "Monaco.Template.Backend.Common.Observability.Boundary";
	internal const string HttpRequestBoundaryKind = "http.request";
	internal const string MessageTemplate = "Unhandled {BoundaryKind} failure ({ExceptionType}); TraceId={TraceId}; SpanId={SpanId}";
	internal static readonly EventId EventId = new(1000, "UnhandledBoundaryException");
	private const string ErrorCategory = "error.category";
	private const string ErrorType = "error.type";

	public static void Record(ILoggerFactory loggerFactory, Exception exception)
	{
		ArgumentNullException.ThrowIfNull(loggerFactory);
		ArgumentNullException.ThrowIfNull(exception);

		var exceptionType = GetBoundedExceptionTypeName(exception.GetType().FullName ?? exception.GetType().Name);
		SanitizeCurrentActivity(exceptionType);

		var activity = Activity.Current;
		loggerFactory.CreateLogger(Category)
					 .LogError(EventId,
							   exception,
							   MessageTemplate,
							   HttpRequestBoundaryKind,
							   exceptionType,
							   activity?.TraceId.ToString() ?? string.Empty,
							   activity?.SpanId.ToString() ?? string.Empty);
	}

	internal static string GetBoundedExceptionTypeName(string typeName)
	{
		ArgumentException.ThrowIfNullOrEmpty(typeName);

		var byteCount = 0;
		var result = new StringBuilder(typeName.Length);
		foreach (var rune in typeName.EnumerateRunes())
		{
			var runeByteCount = Encoding.UTF8.GetByteCount(rune.ToString());
			if (byteCount + runeByteCount > 256)
				break;

			result.Append(rune);
			byteCount += runeByteCount;
		}

		return result.ToString();
	}

	private static void SanitizeCurrentActivity(string exceptionType)
	{
		var activity = Activity.Current;
		if (activity is null)
			return;

		foreach (var tag in activity.TagObjects
									.Where(tag => tag.Key.StartsWith("exception.", StringComparison.Ordinal) ||
												  (tag.Key.StartsWith("error.", StringComparison.Ordinal) &&
												   tag.Key is not ErrorCategory and not ErrorType))
									.Select(tag => tag.Key)
									.ToArray())
			activity.SetTag(tag, null);

		activity.SetStatus(ActivityStatusCode.Error, string.Empty);
		activity.SetTag(ErrorCategory, "unhandled");
		activity.SetTag(ErrorType, exceptionType);
	}
}