using MediatR;
using Microsoft.Extensions.Primitives;

namespace Monaco.Template.Backend.Common.Application.Queries;

/// <summary>
/// Represents a base class for query objects that encapsulate query parameters and provide utility methods for
/// extracting and interpreting query values.
/// </summary>
/// <remarks>This class is designed to be inherited by specific query implementations. It provides access to the
/// query parameters as key-value pairs and includes helper methods for retrieving typed values from the query
/// parameters. The query parameters are case-insensitive for key matching.</remarks>
/// <typeparam name="T">The type of the result expected from the query.</typeparam>
/// <param name="QueryParams">Represents the query parameters collection</param>
public abstract record QueryBase<T>(IEnumerable<KeyValuePair<string, StringValues>> QueryParams) : IRequest<T>
{
	private const string ExpandParam = "expand";
	
	public virtual IEnumerable<KeyValuePair<string, StringValues>> QueryParams { get; } = QueryParams;
	public virtual string?[] Sort => [.. QueryParams.FirstOrDefault(x => x.Key == "sort").Value];

	/// <summary>
	/// Determines whether the specified value is included in the "expand" query parameter.
	/// </summary>
	/// <remarks>The comparison is case-insensitive for both the key and the value.</remarks>
	/// <param name="value">The value to check for in the "expand" query parameter.</param>
	/// <returns><see langword="true"/> if the "expand" query parameter contains the specified value; otherwise, <see
	/// langword="false"/>.</returns>
	protected bool Expand(string value) => QueryParams.Any(x => x.Key.Equals(ExpandParam, StringComparison.InvariantCultureIgnoreCase) &&
																x.Value.Contains(value, StringComparer.InvariantCultureIgnoreCase));

	/// <summary>
	/// Retrieves the first non-null value associated with the specified key from the query parameters.
	/// </summary>
	/// <param name="key">The key to search for in the query parameters. The comparison is case-insensitive and culture-invariant.</param>
	/// <returns>The first non-null value associated with the specified key, or <see langword="null"/> if no such value exists.</returns>
	protected string? GetValueString(string key) => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
															   .Value
															   .FirstOrDefault(x => x is not null);

	protected int? GetValueInt(string key) => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
														 .Value
														 .Select(x => int.TryParse(x, out var y) ? y : (int?)null)
														 .FirstOrDefault(x => x is not null);

	protected long? GetValueLong(string key) => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
														   .Value
														   .Select(x => long.TryParse(x, out var y) ? y : (long?)null)
														   .FirstOrDefault(x => x is not null);

	protected short? GetValueShort(string key) => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
															 .Value
															 .Select(x => short.TryParse(x, out var y) ? y : (short?)null)
															 .FirstOrDefault(x => x is not null);

	protected float? GetValueFloat(string key) => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
															 .Value
															 .Select(x => float.TryParse(x, out var y) ? y : (float?)null)
															 .FirstOrDefault(x => x is not null);

	protected decimal? GetValueDecimal(string key) => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
																 .Value
																 .Select(x => decimal.TryParse(x, out var y) ? y : (decimal?)null)
																 .FirstOrDefault(x => x is not null);

	protected bool? GetValueBool(string key) => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
														   .Value
														   .Select(x => bool.TryParse(x, out var y) ? y : (bool?)null)
														   .FirstOrDefault(x => x is not null);

	protected Guid? GetValueGuid(string key) => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
														   .Value
														   .Select(x => Guid.TryParse(x, out var y) ? y : (Guid?)null)
														   .FirstOrDefault(x => x is not null);

	protected DateTime? GetValueDateTime(string key) => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
																   .Value
																   .Select(x => DateTime.TryParse(x, out var y) ? y : (DateTime?)null)
																   .FirstOrDefault(x => x is not null);

	protected TEnum? GetValueEnum<TEnum>(string key) where TEnum : struct => QueryParams.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
																						.Value
																						.Select(x => Enum.TryParse<TEnum>(x, true, out var y) ? y : (TEnum?)null)
																						.FirstOrDefault(x => x is not null);
}