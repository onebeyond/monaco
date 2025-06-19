namespace Monaco.Template.Backend.Common.Domain.Model;

/// <summary>
/// Represents a paginated subset of items along with metadata about the pagination state.
/// </summary>
/// <remarks>This record is used to encapsulate a collection of items and provide information about the
/// pagination, such as the current offset, limit, and total count of items. It is commonly used in scenarios where data
/// is retrieved in chunks or pages.</remarks>
/// <typeparam name="T">The type of items contained in the page.</typeparam>
public record Page<T>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="Page{T}"/> class with the specified items, offset, limit, and total
	/// count.
	/// </summary>
	/// <remarks>This constructor allows you to create a paginated view of a dataset by specifying the items for the
	/// current page, the offset of the first item, the page size (limit), and the total number of items in the
	/// dataset.</remarks>
	/// <param name="items">The collection of items contained in the page.</param>
	/// <param name="offset">The zero-based index of the first item in the page relative to the entire dataset.</param>
	/// <param name="limit">The maximum number of items that the page can contain.</param>
	/// <param name="count">The total number of items in the entire dataset.</param>
	public Page(IEnumerable<T> items,
				int offset,
				int limit,
				long count)
		: this(items, new Pager(offset,
								limit,
								count))
	{ }

	/// <summary>
	/// Represents a paginated collection of items along with pagination metadata.
	/// </summary>
	/// <param name="items">The collection of items for the current page. Cannot be null.</param>
	/// <param name="pager">The pagination metadata associated with the current page. Cannot be null.</param>
	public Page(IEnumerable<T> items, Pager pager)
	{
		Items = [.. items];
		Pager = pager;
	}

	public Page() { }

	/// <summary>
	/// Page metadata
	/// </summary>
	public Pager Pager { get; init; } = null!;

	/// <summary>
	/// Paged items
	/// </summary>
	public IReadOnlyList<T> Items { get; init; } = null!;
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