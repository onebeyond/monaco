namespace Monaco.Template.Backend.Common.Domain.Model;

public record Page<T>
{
	/// <summary>
	/// </summary>
	/// <param name="items">Items subset</param>
	/// <param name="offset">Record from where to start paging</param>
	/// <param name="limit">Amount of items to page</param>
	/// <param name="count">Total amount of items</param>
	public Page(IEnumerable<T> items, int offset, int limit, long count)
	{
		Items = items.ToList();
		Pager = new Pager(offset, limit, count);
	}

	/// <summary>
	/// Page metadata
	/// </summary>
	public Pager Pager { get; }
	/// <summary>
	/// Paged items
	/// </summary>
	public IReadOnlyList<T> Items { get; }
}

/// <summary>
/// Pagination metadata
/// </summary>
public record Pager(int Offset, int Limit, long Count);