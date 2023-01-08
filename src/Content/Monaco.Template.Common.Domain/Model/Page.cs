namespace Monaco.Template.Common.Domain.Model;

public record Page<T>
{
	/// <summary>
	/// </summary>
	/// <param name="results">Results subset</param>
	/// <param name="offset">Record from where to start paging</param>
	/// <param name="limit">Amount of items to page</param>
	/// <param name="count">Total amount of items</param>
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
public record Pager(int Offset, int Limit, long Count);