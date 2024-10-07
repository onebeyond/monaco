namespace Monaco.Template.Backend.Common.Domain.Model;

public record Page<T>
{
	/// <summary>
	/// </summary>
	/// <param name="items">Items subset</param>
	/// <param name="offset">Record from where to start paging</param>
	/// <param name="limit">Amount of items to page</param>
	/// <param name="count">Total amount of items</param>
	public Page(IEnumerable<T> items,
				int offset,
				int limit,
				long count)
		: this(items,
			   new Pager(offset,
						 limit,
						 count))
	{ }

	public Page(IEnumerable<T> items, Pager pager)
	{
		Items = items.ToList();
		Pager = pager;
	}

	public Page() { }

	/// <summary>
	/// Page metadata
	/// </summary>
	public Pager Pager { get; init; }
	/// <summary>
	/// Paged items
	/// </summary>
	public IReadOnlyList<T> Items { get; init; }
}

/// <summary>
/// Pagination metadata
/// </summary>
public record Pager
{
	public Pager() {}

	/// <summary>
	/// Pagination metadata
	/// </summary>
	public Pager(int offset, int limit, long count)
	{
		Offset = offset;
		Limit = limit;
		Count = count;
	}

	public int Offset { get; init; }
	public int Limit { get; init; }
	public long Count { get; init; }
}