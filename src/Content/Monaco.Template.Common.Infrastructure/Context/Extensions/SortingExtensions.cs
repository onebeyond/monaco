using System.Linq.Expressions;

namespace Monaco.Template.Common.Infrastructure.Context.Extensions;

public static class SortingExtensions
{
    public static IQueryable<T> ApplySort<T>(this IQueryable<T> source, string[] sortFields, string defaultSortField, Dictionary<string, Expression<Func<T, object>>> sortMap)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source), "Data source is empty");

        if (string.IsNullOrWhiteSpace(defaultSortField))
            throw new ArgumentNullException(nameof(defaultSortField), "Default sort field is missing");

        var (sortMapLower, lstSort) = GetData(sortFields, defaultSortField, sortMap);

        var query = source.AsQueryable();
        foreach (var (key, value) in lstSort) //Loop through the fields and apply the sorting
            query = query.GetOrderedQuery(sortMapLower[key], value, key == lstSort.Keys.First());
        return query;
    }

    private static IOrderedQueryable<T> GetOrderedQuery<T>(this IQueryable<T> source, Expression<Func<T, object>> expression, bool ascending, bool firstSort)
    {
        var bodyExpression = (MemberExpression)(expression.Body.NodeType == ExpressionType.Convert ? ((UnaryExpression)expression.Body).Operand : expression.Body);
        var sortLambda = Expression.Lambda(bodyExpression, expression.Parameters);
        Expression<Func<IOrderedQueryable<T>>> sortMethod;
        if (firstSort)
        {
            if (ascending) sortMethod = () => source.OrderBy<T, object>(k => null!);
            else sortMethod = () => source.OrderByDescending<T, object>(k => null!);
        }
        else
        {
            if (ascending) sortMethod = () => ((IOrderedQueryable<T>)source).ThenBy<T, object>(k => null!);
            else sortMethod = () => ((IOrderedQueryable<T>)source).ThenByDescending<T, object>(k => null!);
        }

        var methodCallExpression = (MethodCallExpression)sortMethod.Body;
        var method = methodCallExpression.Method.GetGenericMethodDefinition();
        var genericSortMethod = method.MakeGenericMethod(typeof(T), bodyExpression.Type);
        var orderedQuery = (IOrderedQueryable<T>)genericSortMethod.Invoke(source, new object[] { source, sortLambda })!;
        return orderedQuery;
    }

    public static IEnumerable<T> ApplySort<T>(this IEnumerable<T> source, string[] sortFields, string defaultSortField, Dictionary<string, Expression<Func<T, object>>> sortMap)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source), "Data source is empty");

        if (string.IsNullOrWhiteSpace(defaultSortField))
            throw new ArgumentNullException(nameof(defaultSortField), "Default sort field is missing");

        var (sortMapLower, lstSort) = GetData(sortFields, defaultSortField, sortMap);

        var query = source.AsEnumerable();
        foreach (var (key, value) in lstSort) //Loop through the fields and apply the sorting
            query = query.GetOrderedQuery(sortMapLower[key], value, key == lstSort.Keys.First());
        return query;
    }

    public static (Dictionary<string, Expression<Func<T, object>>> sortMapLower, Dictionary<string, bool> lstSort)
        GetData<T>(IEnumerable<string> sortFields, string defaultSortField, Dictionary<string, Expression<Func<T, object>>> sortMap)
    {
        //convert a Dictionary with Keys into lowercase to ease searching
        var sortMapLower = sortMap.ToDictionary(x => x.Key.ToLower(), x => x.Value);
        //convert the list of fields to sort into a dictionary field/direction and filter out the non existing ones
        var lstSort = ProcessSortParam(sortFields, sortMapLower);
        if (!lstSort.Any()) //if there's none remaining, load the default ones
            lstSort = ProcessSortParam(new[] { defaultSortField }, sortMapLower);

        return (sortMapLower, lstSort);
    }

    private static IOrderedEnumerable<T> GetOrderedQuery<T>(this IEnumerable<T> source, Expression<Func<T, object>> expression, bool ascending, bool firstSort)
    {
        var bodyExpression = (MemberExpression)(expression.Body.NodeType == ExpressionType.Convert ? ((UnaryExpression)expression.Body).Operand : expression.Body);
        var sortLambda = Expression.Lambda(bodyExpression, expression.Parameters);
        Expression<Func<IOrderedEnumerable<T>>> sortMethod;
        if (firstSort)
        {
            if (ascending) sortMethod = () => source.OrderBy<T, object>(k => null!);
            else sortMethod = () => source.OrderByDescending<T, object>(k => null!);
        }
        else
        {
            if (ascending) sortMethod = () => ((IOrderedEnumerable<T>)source).ThenBy<T, object>(k => null!);
            else sortMethod = () => ((IOrderedEnumerable<T>)source).ThenByDescending<T, object>(k => null!);
        }

        if (sortMethod.Body is not MethodCallExpression methodCallExpression)
            throw new Exception("oops");

        var meth = methodCallExpression.Method.GetGenericMethodDefinition();
        var genericSortMethod = meth.MakeGenericMethod(typeof(T), bodyExpression.Type);
        var orderedQuery = (IOrderedEnumerable<T>)genericSortMethod.Invoke(source, new object[] { source, sortLambda.Compile() })!;
        return orderedQuery;
    }

    private static Dictionary<string, bool> ProcessSortParam<T>(IEnumerable<string> sortFields, Dictionary<string, Expression<Func<T, object>>> sortMap)
    {
        return sortFields.ToDictionary(x => (x.StartsWith("-") ? x.Remove(0, 1) : x).ToLower(),
                                       x => !x.StartsWith("-"))
                         .Where(x => sortMap.ContainsKey(x.Key))
                         .ToDictionary(x => x.Key, x => x.Value);
    }
}