using Monaco.Template.Backend.Common.Infrastructure.Persistence;

namespace Monaco.Template.Backend.Worker.Persistence;

public sealed class WorkerPersistenceActorProvider : IPersistenceActorProvider
{
	public static readonly string ServiceActor = $"service:{typeof(Program).Namespace}";

	public string HostRole => PersistenceActor.WorkerHostRole;

	public string GetActor() => ServiceActor;
}