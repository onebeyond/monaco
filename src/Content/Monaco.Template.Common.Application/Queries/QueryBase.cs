using MediatR;
using Microsoft.Extensions.Primitives;

namespace Monaco.Template.Common.Application.Queries;

public abstract record QueryBase<T> : IRequest<T>
{
	protected QueryBase(IEnumerable<KeyValuePair<string, StringValues>> queryString)
	{
		QueryString = queryString;
	}

	public virtual IEnumerable<KeyValuePair<string, StringValues>> QueryString { get; }
	public virtual string[] Sort => QueryString.FirstOrDefault(x => x.Key == "sort").Value.ToArray();

	protected bool Expand(string value) => QueryString.Any(x => x.Key.Equals("expand", StringComparison.InvariantCultureIgnoreCase) &&
																x.Value.Contains(value, StringComparer.InvariantCultureIgnoreCase));

	protected string? GetValueString(string key) => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
															   .Value
															   .FirstOrDefault(x => x is not null);
	
	protected int? GetValueInt(string key) => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
														 .Value
														 .Select(x => int.TryParse(x, out var y) ? y : (int?)null)
														 .FirstOrDefault(x => x is not null);
	
	protected long? GetValueLong(string key) => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
														   .Value
														   .Select(x => long.TryParse(x, out var y) ? y : (long?)null)
														   .FirstOrDefault(x => x is not null);

	protected short? GetValueShort(string key) => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
															 .Value
															 .Select(x => short.TryParse(x, out var y) ? y : (short?)null)
															 .FirstOrDefault(x => x is not null);

	protected float? GetValueFloat(string key) => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
															 .Value
															 .Select(x => float.TryParse(x, out var y) ? y : (float?)null)
															 .FirstOrDefault(x => x is not null);

	protected decimal? GetValueDecimal(string key) => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
																 .Value
																 .Select(x => decimal.TryParse(x, out var y) ? y : (decimal?)null)
																 .FirstOrDefault(x => x is not null);

	protected bool? GetValueBool(string key) => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
														   .Value
														   .Select(x => bool.TryParse(x, out var y) ? y : (bool?)null)
														   .FirstOrDefault(x => x is not null);
	
	protected Guid? GetValueGuid(string key) => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
														   .Value
														   .Select(x => Guid.TryParse(x, out var y) ? y : (Guid?)null)
														   .FirstOrDefault(x => x is not null);
	
	protected DateTime? GetValueDateTime(string key) => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
																   .Value
																   .Select(x => DateTime.TryParse(x, out var y) ? y : (DateTime?)null)
																   .FirstOrDefault(x => x is not null);
	
	protected TEnum? GetValueEnum<TEnum>(string key) where TEnum : struct => QueryString.FirstOrDefault(x => x.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
																						.Value
																						.Select(x => Enum.TryParse<TEnum>(x, true, out var y) ? y : (TEnum?)null)
																						.FirstOrDefault(x => x is not null);
}