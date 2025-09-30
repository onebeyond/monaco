using LinqKit;
using Microsoft.Extensions.Primitives;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Common.Infrastructure.Context.Extensions;

public static class FilterExtensions
{
	/// <summary>
	/// Applies a filter expression to the query by mapping the querystring with the filter map dictionary
	/// </summary>
	/// <typeparam name="T">The type handled by the query</typeparam>
	/// <param name="source">The source query</param>
	/// <param name="queryString">The querystring that provides the fields and their values for the filtering</param>
	/// <param name="filterMap">A dictionary of field names and the expression that maps it against the domain class</param>
	/// <param name="defaultCondition">Indicates if the default condition of the expression will be TRUE or FALSE</param>
	/// <param name="allConditions">Indicates if the filtering must match all the conditions (AND) or just some of them (OR)</param>
	/// <returns>Returns an IQueryable to which has been applied the predicate matching the filtering criteria</returns>
	public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> source,
											   IEnumerable<KeyValuePair<string, StringValues>> queryString,
											   Dictionary<string, Expression<Func<T, object>>> filterMap,
											   bool defaultCondition = true,
											   bool allConditions = true)
	{
		var (filterMapLower, filterList, predicate) = GetData(queryString, filterMap, defaultCondition);

		foreach (var (key, values) in filterList) //and while looping through the list of valid ones to use
		{
			//generate the expression equivalent to that querystring with the mapping corresponding to the DB
			var predicateKey = PredicateBuilder.New<T>(false); //Declare a PredicateBuilder for the current key values
			predicateKey = values.Where(value => ValidateDataType(value, GetBodyExpression(filterMapLower[key]).Type))
								 .Select(value => GetOperationExpression(key, filterMapLower[key], value)) //then generate the expression for each value
								 .Aggregate(predicateKey, (current, expr) => current.Or(expr)); //and chain them all with an OR operator
			predicate = allConditions ? predicate.And(predicateKey) : predicate.Or(predicateKey); //then add the resulting expression to the more general predicate
		}

		return source.Where(predicate);
	}

	/// <summary>
	/// Applies a filter expression to the enumerable by mapping the querystring with the filter map dictionary
	/// </summary>
	/// <typeparam name="T">The type handled by the enumerable</typeparam>
	/// <param name="source">The source enumerable</param>
	/// <param name="queryString">The querystring that provides the fields and their values for the filtering</param>
	/// <param name="filterMap">A dictionary of field names and the expression that maps it against the domain class</param>
	/// <param name="defaultCondition">Indicates if the default condition of the expression will be TRUE or FALSE</param>
	/// <param name="allConditions">Indicates if the filtering must match all the conditions (AND) or just some of them (OR)</param>
	/// <returns>Returns an IEnumerable to which has been applied the predicate matching the filtering criteria</returns>
	public static IEnumerable<T> ApplyFilter<T>(this IEnumerable<T> source,
												IEnumerable<KeyValuePair<string, StringValues>> queryString,
												Dictionary<string, Expression<Func<T, object>>> filterMap,
												bool defaultCondition = true,
												bool allConditions = true)
	{
		var (filterMapLower, filterList, predicate) = GetData(queryString, filterMap, defaultCondition);

		foreach (var (key, values) in filterList) // and while looping through the list of valid ones to use
		{
			var predicateKey = PredicateBuilder.New<T>(false); // Declare a PredicateBuilder for the current key values
			predicateKey = values.Where(value => ValidateDataType(value, GetBodyExpression(filterMapLower[key]).Type))
								 .Select(value => GetOperationExpression(key, filterMapLower[key], value, true)) // then generate the expression for each value
								 .Aggregate(predicateKey, (current, expr) => current.Or(expr));		// and chain them all with an OR operator
			predicate = allConditions ? predicate.And(predicateKey) : predicate.Or(predicateKey);	// then add the resulting expression to the more general predicate
		}

		return source.Where(predicate);
	}

	private static (Dictionary<string, Expression<Func<T, object>>> filterMapLower,
					IEnumerable<KeyValuePair<string, IEnumerable<string?>>>,
					ExpressionStarter<T>)
		GetData<T>(IEnumerable<KeyValuePair<string, StringValues>> queryString,
				   Dictionary<string, Expression<Func<T, object>>> filterMap,
				   bool defaultCondition)
	{
		// convert to Dictionary with Keys in Lowercase to ease search
		var filterMapLower = filterMap.ToDictionary(x => x.Key.ToLower(), x => x.Value);
		// convert field list to filter in a lowercase list, then filter the ones that don't exist and the null ones
		var filterList = queryString.ToDictionary(q => q.Key.ToLower(),
												  q => q.Value.Select(v => v?.ToLower()))
									.Where(x => filterMapLower.ContainsKey(x.Key.ToLower()));
		var predicate = PredicateBuilder.New<T>(defaultCondition); // Generate a PredicateBuilder (True to return all by default)

		return (filterMapLower, filterList, predicate);
	}

	private static Expression GetBodyExpression<T>(Expression<Func<T, object>> expression) =>
		expression.Body.NodeType == ExpressionType.Convert ? ((UnaryExpression)expression.Body).Operand : expression.Body;

	private static Expression<Func<T, bool>> GetOperationExpression<T>(string fieldKey, Expression<Func<T, object>> fieldMap, object? value, bool toLowerCase = false)
	{
		// Obtain expression Type and based on it choose the method for the operation on the DB depending on if it's a String or any other type
		var bodyExpression = GetBodyExpression(fieldMap);
		var type = bodyExpression.Type;

		// Create the call to the method using the selected expression, the method and the value to search. If it's a string and it´s working on an IEnumerable, first apply ToLower
		Expression expression;

		if ((value as string) is not { Length: > 0 }) // Handles comparison against null values
			expression = Expression.Equal(type == typeof(string) || type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)
											  ? bodyExpression                                                                //for strings
											  : Expression.Convert(bodyExpression, typeof(Nullable<>).MakeGenericType(type)), //for all others
										  Expression.Constant(null));
		else if (type == typeof(string)) // Handles string values
		{
			expression = toLowerCase // If needed, applies lower case to entire string to ignore casing differences
							 ? Expression.Call(bodyExpression, type.GetMethod(nameof(string.ToLower), Type.EmptyTypes)!)
							 : bodyExpression;

			var strValue = (string)value;
			var not = strValue is ['!', ..];
			if (not) strValue = strValue[1..];

			if (strValue is ['"', .., '"']) // quoted strings searches as exactly the same
			{
				var constExpr = Expression.Constant(Convert.ChangeType(strValue[1..^1], type));
				expression = not
								 ? Expression.NotEqual(expression, constExpr)
								 : Expression.Equal(expression, constExpr);
			}
			else // otherwise searches with Contains
			{
				expression = Expression.Call(expression,
											 type.GetMethod(nameof(string.Contains), [type])!,
											 Expression.Constant(Convert.ChangeType(strValue, type)));
				if (not) expression = Expression.Not(expression);
			}
		}
		else if (type.IsEnum)
			expression = Expression.Equal(bodyExpression, Expression.Constant(Enum.Parse(type, (string.IsNullOrWhiteSpace(value?.ToString()) ? "0" : value!.ToString()) ?? "0")));
		else if (type.IsAssignableFrom(typeof(Guid))) // Handles Guid values
			expression = value.ToString() is ['!', ..]
							 ? Expression.NotEqual(bodyExpression, Expression.Constant(Guid.Parse(value.ToString()![1..]), type))
							 : Expression.Equal(bodyExpression, Expression.Constant(Guid.Parse(value.ToString()!), type));
		else if (type.IsAssignableFrom(typeof(DateTime)) && fieldKey.EndsWith("from")) // Handles DateTime values whose param name ends with From (range start)
			expression = Expression.GreaterThanOrEqual(bodyExpression, Expression.Constant(DateTime.Parse(value.ToString()!), type));
		else if (type.IsAssignableFrom(typeof(DateTime)) && fieldKey.EndsWith("to")) // Handles DateTime values whose param name ends with To (range end)
			expression = Expression.LessThanOrEqual(bodyExpression, Expression.Constant(DateTime.Parse(value.ToString()!), type));
		else // Handles all other generic cases (numbers, booleans, etc)
		{
			var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
			expression = value.ToString() is ['!', ..]
							 ? Expression.NotEqual(Expression.Convert(bodyExpression, underlyingType), Expression.Constant(Convert.ChangeType(value.ToString()![1..], underlyingType)))
							 : Expression.Equal(Expression.Convert(bodyExpression, underlyingType), Expression.Constant(Convert.ChangeType(value, underlyingType)));
		}

		// Create and return the lambda expression that represents the operation to run
		return Expression.Lambda<Func<T, bool>>(expression, fieldMap.Parameters);
	}

	private static bool ValidateDataType(string? data, Type type)
	{
		data = data is ['!', ..] ? data[1..] : data;
		return data switch
		{
			null or { Length: 0 } => true,
			not null when type == typeof(int) || type == typeof(int?) => int.TryParse(data, out _),
			not null when type == typeof(long) || type == typeof(long?) => long.TryParse(data, out _),
			not null when type == typeof(short) || type == typeof(short?) => short.TryParse(data, out _),
			not null when type == typeof(float) || type == typeof(float?) => float.TryParse(data, out _),
			not null when type == typeof(decimal) || type == typeof(decimal?) => decimal.TryParse(data, out _),
			not null when type == typeof(double) || type == typeof(double?) => double.TryParse(data, out _),
			not null when type == typeof(bool) || type == typeof(bool?) => bool.TryParse(data, out _),
			not null when type == typeof(Guid) || type == typeof(Guid?) => Guid.TryParse(data, out _),
			not null when type == typeof(DateTime) || type == typeof(DateTime?) => DateTime.TryParse(data, out _),
			not null when type.IsEnum => Enum.TryParse(type, data, true, out _),
			_ => type == typeof(string)
		};
	}
}