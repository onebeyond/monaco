using AutoFixture;
using FluentAssertions;
using Moq;
using System;
using System.Diagnostics.CodeAnalysis;
using DcslGs.Template.Common.Domain.Model;
using DcslGs.Template.Common.Domain.Tests.AutoFixture;
using Xunit;

namespace DcslGs.Template.Common.Domain.Tests;

[ExcludeFromCodeCoverage]
public class EnumerationTests
{
    [Trait("Common domain Entities", "Enumeration Entity")]
    [Theory(DisplayName = "New enumeration instance succeeds")]
    [AnonymousData]
    public void NewEnumerationInstanceSucceeds(Guid id, string name)
    {
        var fixture = new Fixture();
        fixture.Customize<Enumeration>(c => c.FromFactory(() => new Mock<Enumeration>(id, name) { CallBase = true }.Object));

        var sut = fixture.Create<Enumeration>();

        sut.Id.Should().Be(id);
        sut.Name.Should().Be(name);
    }

    [Trait("Common domain Entities", "Enumeration Entity")]
    [Theory(DisplayName = "Enumeration to string is name")]
    [AnonymousData]
    public void EnumerationToStringIsName(Guid id, string name)
    {
        var fixture = new Fixture();
        fixture.Customize<Enumeration>(c => c.FromFactory(() => new Mock<Enumeration>(id, name) { CallBase = true }.Object));

        var sut = fixture.Create<Enumeration>();

        sut.ToString().Should().Be(name);
    }

    [Trait("Common domain Entities", "Enumeration Entity")]
    [Fact(DisplayName = "Get all items from Enumeration succeeds")]
    public void GetAllItemsFromEnumerationSucceeds()
    {
        Enumeration.GetAll<DummyEnumerationDerived>()
                   .Should().HaveCount(2).And.Contain(new[] { DummyEnumerationDerived.Item3, DummyEnumerationDerived.Item4 });
    }

    [Trait("Common domain Entities", "Enumeration Entity")]
    [Fact(DisplayName = "Get an enumeration item from its value succeeds")]
    public void GetAnEnumerationItemFromItsValueSucceeds()
    {
        Func<DummyEnumeration> action = () => Enumeration.From<DummyEnumeration>(DummyEnumeration.Item1.Id);
        var result = action.Invoke();

        action.Should().NotThrow();
        result.Should().Be(DummyEnumeration.Item1);
    }

    [Trait("Common domain Entities", "Enumeration Entity")]
    [Theory(DisplayName = "Get an enumeration item from invalid value fails")]
    [AnonymousData]
    public void GetAnEnumerationItemFromInvalidValueFails(Guid id)
    {
        Action action = () => Enumeration.From<DummyEnumeration>(id);

        action.Should().Throw<InvalidOperationException>().WithMessage($"'{id}' is not a valid value in {typeof(DummyEnumeration)}");
    }

    [Trait("Common domain Entities", "Enumeration Entity")]
    [Fact(DisplayName = "Get an enumeration item from its name succeeds")]
    public void GetAnEnumerationItemFromItsNameSucceeds()
    {
        Func<DummyEnumeration> action = () => Enumeration.From<DummyEnumeration>(DummyEnumeration.Item1.Name);
        var result = action.Invoke();

        action.Should().NotThrow();
        result.Should().Be(DummyEnumeration.Item1);
    }

    [Trait("Common domain Entities", "Enumeration Entity")]
    [Theory(DisplayName = "Get an enumeration item from invalid value fails")]
    [AnonymousData]
    public void GetAnEnumerationItemFromInvalidNameFails(string name)
    {
        Action action = () => Enumeration.From<DummyEnumeration>(name);

        action.Should().Throw<InvalidOperationException>().WithMessage($"'{name}' is not a valid display name in {typeof(DummyEnumeration)}");
    }

    [Trait("Common domain Entities", "Enumeration Entity")]
    [Fact(DisplayName = "Enumeration compare succeeds")]
    public void EnumerationCompareSucceds()
    {
        DummyEnumeration.Item1.CompareTo(DummyEnumeration.Item1).Should().Be(0);
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