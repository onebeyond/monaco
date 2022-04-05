using FluentAssertions;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DcslGs.Template.Common.Domain.Model;
using DcslGs.Template.Common.Tests.Factories;
using Xunit;

namespace DcslGs.Template.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
public class PageTests
{
    [Trait("Common domain Entities", "Page Entity")]
    [Theory(DisplayName = "Create a new page succeeds")]
    [AnonymousData]
    public void Create_a_new_page(List<string> results, int offset, int limit, long count)
    {
        var sut = new Page<string>(results, offset, limit, count);

        sut.Pager.Should().NotBeNull().And.BeEquivalentTo(new Pager(offset, limit, count));
        sut.Results.Should().OnlyContain(s => results.Contains(s));
    }
		
    [Trait("Common domain Entities", "Pager Entity")]
    [Theory(DisplayName = "Create a new pager succeeds")]
    [AnonymousData]
    public void Create_a_new_pager(int offset, int limit, long count)
    {
        var sut = new Pager(offset, limit, count);

        sut.Offset.Should().Be(offset);
        sut.Limit.Should().Be(limit);
        sut.Count.Should().Be(count);
    }
}