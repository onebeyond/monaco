using FluentAssertions;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Tests.Factories;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
public class ValueObjectTests
{
	[Trait("Common Domain entities", "ValueObject")]
	[Theory(DisplayName = "New ValueObject instance succeeds")]
	[AnonymousData]
	public void NewValueObjectInstanceSucceeds(string field1, string? field2)
	{
		var sut = new DummyValueObject(field1, field2);

		sut.Field1.Should().Be(field1);
		sut.Field2.Should().Be(field2);
	}

	[Trait("Common Domain entities", "ValueObject")]
	[Theory(DisplayName = "Different ValueObject instances with same values are equal")]
	[AnonymousData]
	public void DifferentValueObjectInstancesWithSameValuesAreEqual(string field1, string? field2)
	{
		var val1 = new DummyValueObject(field1, field2);
		var val2 = new DummyValueObject(field1, field2);

		(val1 == val2).Should().BeTrue();
		val1.Equals(val2).Should().BeTrue();
	}

	[Trait("Common Domain entities", "ValueObject")]
	[Theory(DisplayName = "Different ValueObject instances with same values are not equal")]
	[AnonymousData]
	public void DifferentValueObjectInstancesWithDifferentValuesAreNotEqual(string field1, string? field2, string? field3)
	{
		var val1 = new DummyValueObject(field1, field2);
		var val2 = new DummyValueObject(field1, field3);

		(val1 != val2).Should().BeTrue();
		val1.Equals(val2).Should().BeFalse();
	}

	[Trait("Common Domain entities", "ValueObject")]
	[Theory(DisplayName = "Different ValueObject instances with same values are not equal")]
	[AnonymousData]
	public void ValueObjectComparedAgainstNullIsNotEqual(string field1, string? field2)
	{
		var sut = new DummyValueObject(field1, field2);

		// ReSharper disable once ConditionIsAlwaysTrueOrFalse
		(sut == null).Should().BeFalse();
		sut!.Equals(null).Should().BeFalse();
	}

	#region Dummy ValueObjects

	public class DummyValueObject : ValueObject
	{
		public DummyValueObject(string field1, string? field2 = null)
		{
			Field1 = field1;
			Field2 = field2;
		}

		public string Field1 { get; }
		public string? Field2 { get; }

		protected override IEnumerable<object?> GetEqualityComponents()
		{
			yield return Field1;
			yield return Field2;
		}
	}

	#endregion
}