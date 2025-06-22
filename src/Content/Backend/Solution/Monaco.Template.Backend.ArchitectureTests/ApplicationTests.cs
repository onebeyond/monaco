using ArchUnitNET.Fluent.Syntax.Elements.Types.Classes;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Monaco.Template.Backend.ArchitectureTests.Extensions;
using Monaco.Template.Backend.Common.Application.Commands;
using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Infrastructure.EntityConfigurations;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
[Trait("Architecture Tests", "Application")]
public class ApplicationTests : BaseTest
{
	private static readonly Architecture Architecture = new ArchLoader().LoadAssemblies(ApplicationAssembly,
																						CommonApplicationAssembly,
																						DomainAssembly,
																						CommonDomainAssembly)
																		.Build();

	private readonly IObjectProvider<IType> _applicationLayer = Types().That()
																	   .ResideInAssembly(ApplicationAssembly)
																	   .As("Application Layer");

	private readonly GivenClassesConjunction _commandAndQueries = Classes().That()
																		   .ImplementInterface(typeof(IRequest))
																		   .Or()
																		   .ImplementInterface(typeof(IRequest<>))
																		   .And()
																		   .AreNotAbstract();

	private static readonly Class CommandBase = Architecture.GetClassOfType(typeof(CommandBase));
	private static readonly Class CommandBaseReturn = Architecture.GetClassOfType(typeof(CommandBase<>));

	private readonly GivenClassesConjunction _commands = Classes().That()
																  .AreAssignableTo(CommandBase)
																  .Or()
																  .AreAssignableTo(CommandBaseReturn)
																  .And()
																  .AreNotAbstract();

	private static readonly Interface RequestHandler = Architecture.GetInterfaceOfType(typeof(IRequestHandler<>));
	private static readonly Interface RequestHandlerReturn = Architecture.GetInterfaceOfType(typeof(IRequestHandler<,>));

	private readonly GivenClassesConjunction _handlers = Classes().That()
																  .ImplementInterface(RequestHandler)
																  .Or()
																  .ImplementInterface(RequestHandlerReturn)
																  .And()
																  .AreNotAbstract();

	private static readonly Class Validator = Architecture.GetClassOfType(typeof(AbstractValidator<>));

	private readonly GivenClassesConjunction _validators = Classes().That()
																	.AreAssignableTo(Validator)
																	.And()
																	.AreNotAbstract();

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


	[Fact(DisplayName = "Commands and Queries exist in Application layer and are records, public, nested, sealed and have name Command or Query")]
	public void CommandsAndQueriesAreRecordsPublicNestedSealedAndNamedCommandQuery() =>
		_commandAndQueries.Should()
						  .Be(_applicationLayer)
						  .AndShould()
						  .BeRecord()
						  .AndShould()
						  .BePublic()
						  .AndShould()
						  .BeNested()
						  .AndShould()
						  .BeSealed()
						  .AndShould()
						  .HaveName("Command")
						  .OrShould()
						  .HaveName("Query")
						  .Check(Architecture);

	[Fact(DisplayName = "Commands have validators")]
	public void CommandsHaveValidators() =>
		_commands.Should()
				 .FollowCustomCondition(command => _validators.GetObjects(Architecture)
															  .Any(v => v.GetInheritsBaseClassDependencies()
																		 .Any(d => d.Target.Equals(Validator) &&
																				   d.TargetGenericArguments
																					.Any(g => g.Type.Equals(command)))),
										"have a validator",
										"does not have a validator")
				 .Because("every command derived from CommandBase needs to have a validator")
				 .Check(Architecture);

	[Fact(DisplayName = "Commands do not invoke other commands")]
	public void CommandsDontInvokeOtherCommands() =>
		_handlers.Should()
				 .NotDependOnAnyTypesThat()
				 .Are(typeof(ISender))
				 .AndShould()
				 .NotDependOnAnyTypesThat()
				 .ImplementInterface(typeof(ISender))
				 .Because("it can be used to invoke other commands and it should not")
				 .Check(Architecture);

	[Fact(DisplayName = "Validators exist in Application layer and are sealed, internal and nested")]
	public void ValidatorsExistInApplicationAreSealedInternalAndNested() =>
		_validators.Should()
				   .Be(_applicationLayer)
				   .AndShould()
				   .HaveName("Validator")
				   .AndShould()
				   .BeSealed()
				   .AndShould()
				   .BeInternal()
				   .AndShould()
				   .BeNested()
				   .Check(Architecture);

	[Fact(DisplayName = "Handlers exist in Application layer, are named Handler and are nested, sealed and internal")]
	public void HandlersExistInApplicationAreNamedHandlerAndAreNestedSealedAndInternal() =>
		_handlers.Should()
				 .Be(_applicationLayer)
				 .AndShould()
				 .HaveName("Handler")
				 .AndShould()
				 .BeNested()
				 .AndShould()
				 .BeSealed()
				 .AndShould()
				 .BeInternal()
				 .Check(Architecture);

	[Fact(DisplayName = "Command, validator and handler should be nested together")]
	public void CommandValidatorAndHandlerAreNestedTogether() =>
		_commands.Should()
				 .FollowCustomCondition(c => _validators.GetObjects(Architecture)
														.Single(v => v.GetInheritsBaseClassDependencies()
																	  .Any(d => d.Target.Equals(Validator) &&
																				d.TargetGenericArguments
																				 .Any(g => g.Type.Equals(c))))
														.NestType(Architecture)!
														.Equals(c.NestType(Architecture)),
										"be nested together with their validator",
										"is not nested together with its validator")
				 .AndShould()
				 .FollowCustomCondition(c => _handlers.GetObjects(Architecture)
													  .Single(h => h.GetImplementsInterfaceDependencies()
																	.Any(i => i.Target.Equals(RequestHandlerReturn) &&
																			  i.TargetGenericArguments
																			   .Any(g => g.Type.Equals(c))))
													  .NestType(Architecture)!
													  .Equals(c.NestType(Architecture)),
										"be nested together with their handler",
										"is not nested together with its handler")
				 .Because("having each feature as a single file component reduces boilerplate and improves DX")
				 .Check(Architecture);

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