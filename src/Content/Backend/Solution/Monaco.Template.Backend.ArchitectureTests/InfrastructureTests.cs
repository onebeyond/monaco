using ArchUnitNET.Fluent.Syntax.Elements.Types.Classes;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.EntityConfigurations;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Infrastructure")]
public class InfrastructureTests : BaseTest
{
	private static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(ApplicationAssembly,
																						CommonApplicationAssembly,
																						DomainAssembly,
																						CommonDomainAssembly,
																						InfrastructureAssembly)
																		.Build();

	private readonly IObjectProvider<IType> _applicationLayer = Types().That()
																	   .ResideInAssembly(ApplicationAssembly,
																						 InfrastructureAssembly)
																	   .As("Application Layer");

	private static readonly Class AggregateRoot = Architecture.GetClassOfType(typeof(AggregateRoot));
	private static readonly Class Entity = Architecture.GetClassOfType(typeof(Entity));
	private static readonly Class Enumeration = Architecture.GetClassOfType(typeof(Enumeration));

	private readonly GivenClassesConjunctionWithDescription _entities = Classes().That()
																				 .AreAssignableTo(Entity)
																				 .And()
																				 .AreNot(Entity)
																				 .And()
																				 .AreNot(AggregateRoot)
																				 .Or()
																				 .AreAssignableTo(Enumeration)
																				 .And()
																				 .AreNot(Enumeration)
																				 .As("Entities");

	private static readonly Interface EntityTypeConfiguration = Architecture.GetInterfaceOfType(typeof(IEntityTypeConfiguration<>));
	private static readonly Class EntityTypeConfigurationBase = Architecture.GetClassOfType(typeof(EntityTypeConfigurationBase<>));

	private readonly GivenClassesConjunctionWithDescription _entityConfiguration = Classes().That()
																							.AreAssignableTo(EntityTypeConfiguration)
																							.And()
																							.AreNotAbstract()
																							.As("Entity Configurations");

	[Fact(DisplayName = "EntityConfigurations should exist in Application layer and be internal and sealed")]
	public void EntityConfigurationsExistInApplicationLayerAndBeInternalAndSealed() =>
		_entityConfiguration.Should()
							.Be(_applicationLayer)
							.AndShould()
							.BeInternal()
							.AndShould()
							.BeSealed()
							.Check(Architecture);

	[Fact(DisplayName = "Entities have EntityConfiguration")]
	public void EntitiesHaveEntityConfiguration() =>
		_entities.Should()
				 .FollowCustomCondition(c => _entityConfiguration.GetObjects(Architecture)
																 .Any(etc => etc.GetImplementsInterfaceDependencies()
																				.Any(i => i.Target.Equals(EntityTypeConfiguration) &&
																						  i.TargetGenericArguments
																						   .Any(g => g.Type.Equals(c))) ||
																			 etc.GetInheritsBaseClassDependencies()
																				.Any(b => b.Target.Equals(EntityTypeConfigurationBase) &&
																						  b.TargetGenericArguments
																						   .Any(g => g.Type.Equals(c)))),
										"have their corresponding EntityTypeConfiguration",
										"does not have its corresponding EntityTypeConfiguration")
				 .Because("each entity should be explicitly configured in EF Core")
				 .WithoutRequiringPositiveResults()
				 .Check(Architecture);
}