using System.Linq.Expressions;
using LinqKit;
using Microsoft.Extensions.Primitives;

namespace DcslGs.Template.Common.Infrastructure.Context.Extensions;

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
    public static IQueryable<T> ApplyFilter<T>(this IQueryable<T> source, IEnumerable<KeyValuePair<string, StringValues>> queryString,
                                               Dictionary<string, Expression<Func<T, object>>> filterMap,
                                               bool defaultCondition = true, bool allConditions = true)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source), "Data source is empty");

        var (filterMapLower, filterList, predicate) = GetData(queryString, filterMap, defaultCondition);

        foreach (var (key, values) in filterList) //and while looping through the list of valid ones to use
        {       //genenerate the expression equivalent to that queystring with the mapping corresponding to the DB
            var predicateKey = PredicateBuilder.New<T>(false);  //Declare a PredicateBuilder for the current key values
            predicateKey = values.Select(value => GetOperationExpression(key, filterMapLower[key], value))  //then generate the expresion for each value
                                 .Aggregate(predicateKey, (current, expr) => current.Or(expr));     //and chain them all with an OR operator
            predicate = allConditions ? predicate.And(predicateKey) : predicate.Or(predicateKey); //then add the resulting expression to the more general predicate
        }
        return source.Where(predicate).AsExpandable();
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
    public static IEnumerable<T> ApplyFilter<T>(this IEnumerable<T> source, IEnumerable<KeyValuePair<string, StringValues>> queryString,
                                                Dictionary<string, Expression<Func<T, object>>> filterMap,
                                                bool defaultCondition = true, bool allConditions = true)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source), "Data source is empty");

        var (filterMapLower, filterList, predicate) = GetData(queryString, filterMap, defaultCondition);

        foreach (var (key, values) in filterList) //and while looping through the list of valid ones to use
        {
            var predicateKey = PredicateBuilder.New<T>(false);  //Declare a PredicateBuilder for the current key values
            predicateKey = values.Select(value => GetOperationExpression(key, filterMapLower[key], value, true))  //then generate the expresion for each value
                                 .Aggregate(predicateKey, (current, expr) => current.Or(expr));     //and chain them all with an OR operator
            predicate = allConditions ? predicate.And(predicateKey) : predicate.Or(predicateKey); //then add the resulting expresion to the more general predicate
        }

        return source.Where(predicate);
    }

    private static (Dictionary<string, Expression<Func<T, object>>> filterMapLower,
        IEnumerable<KeyValuePair<string, IEnumerable<string>>>,
        ExpressionStarter<T>)
        GetData<T>(IEnumerable<KeyValuePair<string, StringValues>> queryString,
                   Dictionary<string, Expression<Func<T, object>>> filterMap,
                   bool defaultCondition)
    {
        //convert to Dictionary with Keys in Lowercase to ease search
        var filterMapLower = filterMap.ToDictionary(x => x.Key.ToLower(), x => x.Value);
        //convert field list to filter in a lowercase list, then filter the ones that don't exist and the null ones
        var filterList = queryString.ToDictionary(q => q.Key.ToLower(),
                                                  q => q.Value.Select(v => v?.ToLower()))
                                    .Where(x => filterMapLower.ContainsKey(x.Key.ToLower()));
        var predicate = PredicateBuilder.New<T>(defaultCondition); //Generate a PredicateBuilder (True to return all by default)

        return (filterMapLower, filterList, predicate);
    }

    private static Expression<Func<T, bool>> GetOperationExpression<T>(string fieldKey, Expression<Func<T, object>> fieldMap, object value, bool toLowerCase = false)
    {
        //Obtain expression Type and based on it y choose the method for the operation on the DB depending on if it's a String or any other type
        var bodyExpression = fieldMap.Body.NodeType == ExpressionType.Convert ? ((UnaryExpression)fieldMap.Body).Operand : fieldMap.Body;
        var type = bodyExpression.Type;

        //Create the call to the method using the selected expression, the method and the value to search. If it's a string and it´s working on an IEnumerable, first apply ToLower
        Expression expression;

        if (string.IsNullOrEmpty(value as string))
            expression = Expression.Equal(bodyExpression, Expression.Constant(null));
        else if (type == typeof(string))    //Handles string values
        {
            expression = toLowerCase    //If it's needed, applies lower case to entire string to ignore casing differences
                             ? Expression.Call(bodyExpression, type.GetMethod("ToLower", new Type[0]))
                             : bodyExpression;

            var strValue = (string)value;
            var not = strValue.StartsWith('!');
            if (not) strValue = strValue[1..];

            if (strValue.StartsWith('"') && strValue.EndsWith('"'))
                expression = not ? Expression.NotEqual(expression, Expression.Constant(Convert.ChangeType(strValue[1..^1], type)))
                                 : Expression.Equal(expression, Expression.Constant(Convert.ChangeType(strValue[1..^1], type)));
            else
            {
                expression = Expression.Call(expression,
                                             type.GetMethod("Contains", new[] { type }),
                                             Expression.Constant(Convert.ChangeType(strValue, type)));
                if (not) expression = Expression.Not(expression);
            }
        }
        else if (type.IsAssignableFrom(typeof(Guid)))   //Handles Guid values
            expression = value.ToString().StartsWith('!')
                             ? Expression.NotEqual(bodyExpression, Expression.Constant(Guid.Parse(value.ToString()[1..]), type))
                             : Expression.Equal(bodyExpression, Expression.Constant(Guid.Parse(value.ToString()), type));
        else if (type.IsAssignableFrom(typeof(DateTime)) && fieldKey.EndsWith("from"))  //Handles DateTime values whose param name ends with From (range start)
            expression = Expression.GreaterThanOrEqual(bodyExpression, Expression.Constant(DateTime.Parse(value.ToString()), type));
        else if (type.IsAssignableFrom(typeof(DateTime)) && fieldKey.EndsWith("to"))    //Handles DateTime values whose param name ends with To (range end)
            expression = Expression.LessThanOrEqual(bodyExpression, Expression.Constant(DateTime.Parse(value.ToString()), type));
        else    //Handles all other generic cases (numbers, booleans, etc)
            expression = value.ToString().StartsWith('!')
                             ? Expression.NotEqual(bodyExpression, Expression.Constant(Convert.ChangeType(value.ToString()[1..], type)))
                             : Expression.Equal(bodyExpression, Expression.Constant(Convert.ChangeType(value, type)));

        //Create and return the lambda expression that represents the operation to run
        return Expression.Lambda<Func<T, bool>>(expression, fieldMap.Parameters);
    }
}