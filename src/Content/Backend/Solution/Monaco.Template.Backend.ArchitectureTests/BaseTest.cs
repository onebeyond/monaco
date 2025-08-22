using Assembly = System.Reflection.Assembly;

namespace Monaco.Template.Backend.ArchitectureTests;

[ExcludeFromCodeCoverage]
public abstract class BaseTest
{
	protected static readonly Assembly ApplicationAssembly = Assembly.Load("Monaco.Template.Backend.Application");
	protected static readonly Assembly DomainAssembly = Assembly.Load("Monaco.Template.Backend.Domain");
	protected static readonly Assembly CommonApplicationAssembly = Assembly.Load("Monaco.Template.Backend.Common.Application");
	protected static readonly Assembly CommonDomainAssembly = Assembly.Load("Monaco.Template.Backend.Common.Domain");
}