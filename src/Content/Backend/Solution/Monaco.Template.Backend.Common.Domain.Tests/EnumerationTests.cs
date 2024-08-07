using FluentAssertions;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Domain.Tests.Factories;
using Monaco.Template.Backend.Common.Domain.Tests.Factories.Entities;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Domain Entities", "Enumeration Entity")]
public class EnumerationTests
{
	[Theory(DisplayName = "New enumeration instance succeeds")]
	[AutoDomainData]
	public void NewEnumerationInstanceSucceeds(Guid id, string name)
	{
		var sut = EnumerationFactory.CreateMock((id, name));

		sut.Id
		   .Should()
		   .Be(id);
		sut.Name
		   .Should()
		   .Be(name);
	}

	[Theory(DisplayName = "Enumeration to string is name")]
	[AutoDomainData]
	public void EnumerationToStringIsName(Guid id, string name)
	{
		var sut = EnumerationFactory.CreateMock((id, name));

		sut.ToString()
		   .Should()
		   .Be(name);
	}

	[Fact(DisplayName = "Get all items from Enumeration succeeds")]
	public void GetAllItemsFromEnumerationSucceeds() =>
		Enumeration.GetAll<DummyEnumerationDerived>()
				   .Should()
				   .HaveCount(2)
				   .And
				   .Contain([DummyEnumerationDerived.Item3, DummyEnumerationDerived.Item4]);

	[Fact(DisplayName = "Get an enumeration item from its value succeeds")]
	public void GetAnEnumerationItemFromItsValueSucceeds()
	{
		var action = () => Enumeration.From<DummyEnumeration>(DummyEnumeration.Item1.Id);
		var result = action.Invoke();

		action.Should()
			  .NotThrow();
		result.Should()
			  .Be(DummyEnumeration.Item1);
	}

	[Theory(DisplayName = "Get an enumeration item from invalid value fails")]
	[AutoDomainData]
	public void GetAnEnumerationItemFromInvalidValueFails(Guid id)
	{
		Action action = () => Enumeration.From<DummyEnumeration>(id);

		action.Should()
			  .Throw<InvalidOperationException>()
			  .WithMessage($"'{id}' is not a valid value in {typeof(DummyEnumeration)}");
	}

	[Fact(DisplayName = "Get an enumeration item from its name succeeds")]
	public void GetAnEnumerationItemFromItsNameSucceeds()
	{
		var action = () => Enumeration.From<DummyEnumeration>(DummyEnumeration.Item1.Name);
		var result = action.Invoke();

		action.Should()
			  .NotThrow();
		result.Should()
			  .Be(DummyEnumeration.Item1);
	}

	[Theory(DisplayName = "Get an enumeration item from invalid value fails")]
	[AutoDomainData]
	public void GetAnEnumerationItemFromInvalidNameFails(string name)
	{
		var action = () => Enumeration.From<DummyEnumeration>(name);

		action.Should()
			  .Throw<InvalidOperationException>()
			  .WithMessage($"'{name}' is not a valid display name in {typeof(DummyEnumeration)}");
	}

	[Fact(DisplayName = "Enumeration compare succeeds")]
	public void EnumerationCompareSucceds() =>
		DummyEnumeration.Item1
						.CompareTo(DummyEnumeration.Item1)
						.Should()
						.Be(0);

	#region Dummy Enumerations

	public class DummyEnumeration(Guid id, string name) : Enumeration(id, name)
	{
		public static DummyEnumeration Item1 = new(new Guid("C176B6CE-F931-4AD5-A61E-DAD4A01180E6"), nameof(Item1));
		public static DummyEnumeration Item2 = new(new Guid("2281FE14-3548-4060-B503-3B26BCFD0000"), nameof(Item2));
	}

	public class DummyEnumerationDerived(Guid id, string name) : DummyEnumeration(id, name)
	{
		public static DummyEnumerationDerived Item3 = new(new Guid("1724C39A-E51C-46BB-9DB9-1227E9EDD406"), nameof(Item3));
		public static DummyEnumerationDerived Item4 = new(new Guid("30B0DC89-E29C-4867-A17D-3AF74695ECC0"), nameof(Item4));
		private static DummyEnumerationDerived Item5 = new(new Guid("0EBC0704-58E0-4202-8CBB-D818A3FDFB5A"), nameof(Item5));
	}

	#endregion
}