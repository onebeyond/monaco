using AwesomeAssertions;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Domain.Tests.Factories;
using Monaco.Template.Backend.Common.Domain.Tests.Factories.Entities;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Monaco.Template.Backend.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
[Trait("Common Domain Entities", "Base Entity")]
public class EntityTests
{
	[Theory(DisplayName = "New entity without parameters succeeds")]
	[AutoDomainData]
	public void NewEntityWithoutParametersSucceeds(Entity sut)
	{
		sut.Id
		   .Should()
		   .BeEmpty();
	}

	[Theory(DisplayName = "New entity with parameters succeeds")]
	[AutoDomainData]
	public void NewEntityWithParametersSucceeds(Guid id)
	{
		var sut = EntityFactory.CreateMock(id);

		sut.Id
		   .Should()
		   .Be(id);
	}

	[Theory(DisplayName = "Entity instance equals (method) itself succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodItselfSucceeds(Entity sut) =>
		sut.Equals(sut)
		   .Should()
		   .BeTrue();

	[Theory(DisplayName = "Entity instance equals (method) another instance same Id as same succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodAnotherInstanceSameIdAsSameSucceeds(Guid id)
	{
		var sut = EntityFactory.CreateMock(id);
		var instance = EntityFactory.CreateMock(id);

		sut.Equals(instance)
		   .Should()
		   .BeTrue();
	}

	[Theory(DisplayName = "Entity instance equals (method) another instance as different succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodAnotherInstanceAsDifferentSucceeds(Entity sut, Entity instance) =>
		sut.Equals(instance)
		   .Should()
		   .BeFalse();

	[Theory(DisplayName = "Entity instance equals (method) another different object as different succeeds ")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodAnotherDifferentObjectAsDifferentSucceeds(Entity sut, object instance) =>
		sut.Equals(instance)
		   .Should()
		   .BeFalse();

	[Theory(DisplayName = "Entity instance equals (method) another different not proxy object as different succeeds ")]
	[AutoDomainData]
	public void EntityInstanceEqualsMethodAnotherDifferentNotProxyObjectAsDifferentSucceeds(Entity sut) =>
		sut.Equals(new object())
		   .Should()
		   .BeFalse();

	[Theory(DisplayName = "Entity instance equals (operator) itself succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsOperatorItselfSucceeds(Entity sut) =>
#pragma warning disable CS1718 // Comparison made to same variable
		(sut == sut).Should()
					.BeTrue();
#pragma warning restore CS1718 // Comparison made to same variable


	[Theory(DisplayName = "Entity instance equals (operator) as different succeeds")]
	[AutoDomainData]
	public void EntityInstanceEqualsOperatorNullAsDifferentSucceeds(Entity sut) =>
		(sut == null).Should()
					 .BeFalse();

	[Fact(DisplayName = "Entity instance null equals (operator) null as same succeeds")]
	public void EntityInstanceNullEqualsOperatorNullAsSameSucceeds() =>
		(null as Entity == null).Should()
								.BeTrue();

	[Theory(DisplayName = "Entity instance equals (operator) null as different succeeds")]
	[AutoDomainData]
	public void EntityInstanceNotEqualsOperatorNullAsDifferentSucceeds(Entity? sut) =>
		(sut != null).Should()
					 .BeTrue();

	[Theory(DisplayName = "Get Entity hash code succeeds")]
	[AutoDomainData]
	public void EntityGetHashCodeSucceeds(Entity sut)
	{
		var hashCode = HashCode.Combine(typeof(Entity), sut.Id);

		sut.GetHashCode()
		   .Should()
		   .Be(hashCode);
	}
}