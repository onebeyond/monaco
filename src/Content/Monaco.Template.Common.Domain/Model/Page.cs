namespace Monaco.Template.Common.Domain.Model;

public class Page<T>
{
    /// <summary>
    /// </summary>
    /// <param name="results"></param>
    /// <param name="offset"></param>
    /// <param name="limit"></param>
    /// <param name="count"></param>
    public Page(IEnumerable<T> results, int offset, int limit, long count)
    {
        Results = results.ToList();
        Pager = new Pager(offset, limit, count);
    }

    /// <summary>
    /// Page metadata
    /// </summary>
    public Pager Pager { get; }
    /// <summary>
    /// Paged results
    /// </summary>
    public IReadOnlyList<T> Results { get; }
}

/// <summary>
/// Pagination metadata
/// </summary>
public class Pager
{
    public Pager(int offset, int limit, long count)
    {
        Offset = offset;
        Limit = limit;
        Count = count;
    }

    /// <summary>
    /// Record from where to start paging
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Amount of items to page
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Total amount of items
    /// </summary>
    public long Count { get; }
}