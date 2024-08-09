using FluentAssertions;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Domain.Tests.Factories;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
public class PageTests
{
	[Trait("Common Domain Entities", "Page Entity")]
	[Theory(DisplayName = "Create a new page succeeds")]
	[AutoDomainData]
	public void CreateNewPageSucceeds(List<string> results,
									  int offset,
									  int limit,
									  long count)
	{
		var sut = new Page<string>(results, offset, limit, count);

		sut.Pager
		   .Should()
		   .NotBeNull()
		   .And
		   .BeEquivalentTo(new Pager(offset, limit, count));
		sut.Items
		   .Should()
		   .OnlyContain(s => results.Contains(s));
	}

	[Trait("Common Domain Entities", "Pager Entity")]
	[Theory(DisplayName = "Create a new pager succeeds")]
	[AutoDomainData]
	public void CreateNewPagerSucceeds(int offset,
									   int limit,
									   long count)
	{
		var sut = new Pager(offset, limit, count);

		sut.Offset
		   .Should()
		   .Be(offset);
		sut.Limit
		   .Should()
		   .Be(limit);
		sut.Count
		   .Should()
		   .Be(count);
	}
}