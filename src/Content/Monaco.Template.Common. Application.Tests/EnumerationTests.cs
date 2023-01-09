using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Xunit;
using Monaco.Template.Common.Application.DTOs;
using Monaco.Template.Common.Application.DTOs.Extensions;
using Monaco.Template.Common.Domain.Model;

namespace Monaco.Template.Common._Application.Tests;

[ExcludeFromCodeCoverage]
public class EnumerationTests
{
	[Trait("Common application entities", "Enumeration Entity")]
	[Fact(DisplayName = "Mapping a Enumerable Referential Type succeeds")]
	public void MapEnumerationSucceeds()
	{
		var item = DummyEnumeration.Item1;
       
		var sut = item.Map<DummyEnumeration, BaseTypeDto>();

		sut.Should().BeOfType<BaseTypeDto>();
		sut.Id.Should().Be(item.Id);
		sut.Name.Should().Be(item.Name);
	}
    
	[Trait("Common application entities", "Enumeration Entity")]
	[Fact(DisplayName = "Mapping a Enumerable Referential with null value succeeds")]
	public void MapEnumerationSucceedsWithNullValue()
	{
		DummyEnumeration item = null;
       
		var sut = item.Map<DummyEnumeration, BaseTypeDto>();
		sut.Should().Be(null);
	}
    
	#region Dummy Enumerations

	public class DummyEnumeration : Enumeration
	{
		public static DummyEnumeration Item1 = new(new Guid("C176B6CE-F931-4AD5-A61E-DAD4A01180E6"), nameof(Item1));
		public static DummyEnumeration Item2 = new(new Guid("2281FE14-3548-4060-B503-3B26BCFD0000"), nameof(Item2));

		public DummyEnumeration(Guid id, string name) : base(id, name)
		{
		}
	}

	public class DummyEnumerationDerived : DummyEnumeration
	{
		public static DummyEnumerationDerived Item3 = new(new Guid("1724C39A-E51C-46BB-9DB9-1227E9EDD406"), nameof(Item3));
		public static DummyEnumerationDerived Item4 = new(new Guid("30B0DC89-E29C-4867-A17D-3AF74695ECC0"), nameof(Item4));
		private static DummyEnumerationDerived Item5 = new(new Guid("0EBC0704-58E0-4202-8CBB-D818A3FDFB5A"), nameof(Item5));

		public DummyEnumerationDerived(Guid id, string name) : base(id, name)
		{
		}
	}

	#endregion
}