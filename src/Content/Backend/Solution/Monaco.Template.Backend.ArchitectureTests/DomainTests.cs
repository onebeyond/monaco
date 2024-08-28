using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Extensions;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Monaco.Template.Backend.ArchitectureTests.Extensions;
using Monaco.Template.Backend.Common.Domain.Model;
using System.Diagnostics.CodeAnalysis;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Domain")]
public class DomainTests : BaseTest
{
	private static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(DomainAssembly,
																						CommonDomainAssembly)
																		.Build();

	private readonly IObjectProvider<IType> _domainLayer = Types().That()
																  .ResideInAssembly(DomainAssembly)
																  .As("Domain Layer");

	private static readonly Class Entity = Architecture.GetClassOfType(typeof(Entity));
	private static readonly Class Enumeration = Architecture.GetClassOfType(typeof(Enumeration));
	private static readonly Class ValueObject = Architecture.GetClassOfType(typeof(ValueObject));

	[Fact(DisplayName = "Entities exist only in Domain layer and don't have public or internal property setters")]
	public void EntitiesExistOnlyInDomainLayerAndHaveNoPublicOrInternalPropSetters() =>
		Classes().That()
				 .AreAssignableTo(Entity)
				 .And()
				 .AreNot(Entity)
				 .And()
				 .AreNotAssignableTo(Enumeration)
				 .Should()
				 .Be(_domainLayer)
				 .Because("Entities should only belong to the Domain layer and exist in there")
				 .AndShould()
				 .NotHavePropertySetterWithVisibility(Visibility.Public,
													  Visibility.Internal,
													  Visibility.ProtectedInternal)
				 .Check(Architecture);

	[Fact(DisplayName = "ValueObjects exist only in Domain layer and are immutable")]
	public void ValueObjectsExistOnlyInDomainLayerAndAreImmutable() =>
		Classes().That()
				 .AreAssignableTo(ValueObject)
				 .And()
				 .AreNot(ValueObject)
				 .Should()
				 .Be(_domainLayer)
				 .Because("ValueObjects should only belong to the Domain layer and exist in there")
				 .AndShould()
				 .BeImmutable()
				 .Check(Architecture);

	[Fact(DisplayName = "Enumerations exist only in Domain layer and are immutable")]
	public void EnumerationsExistOnlyInDomainLayerAndAreImmutable() =>
		Classes().That()
				 .AreAssignableTo(Enumeration)
				 .And()
				 .AreNot(Enumeration)
				 .Should()
				 .Be(_domainLayer)
				 .Because("Enumerations should only belong to the Domain layer and exist in there")
				 .AndShould()
				 .BeImmutable()
				 .WithoutRequiringPositiveResults()		// It's recommended to remove this by the time the generated solution actually contains an entity implementing Enumeration
				 .Check(Architecture);
}