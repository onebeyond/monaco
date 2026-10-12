using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Monaco.Eng.RuntimeShapeVerification.WorkerHook;

public static class GeneratedCompanyStamp
{
	public static async Task<Guid> SaveAsync(IServiceProvider services,
											 CancellationToken cancellationToken,
											 string? assemblyPrefix = null)
	{
		var dbContext = ResolveAppDbContext(services, assemblyPrefix);
		var companyType = FindType("Company", ".Domain.", assemblyPrefix);
		var ctor = companyType.GetConstructors().OrderByDescending(candidate => candidate.GetParameters().Length).First();
		var parameters = ctor.GetParameters();
		var args = new object?[parameters.Length];
		args[0] = $"stamp-{Guid.NewGuid():N}";
		args[1] = $"{Guid.NewGuid():N}@example.test";
		for (var i = 2; i < parameters.Length; i++)
			args[i] = parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null;

		var company = ctor.Invoke(args);
		var setMethod = dbContext.GetType()
								 .GetMethods()
								 .First(method => method is { Name: "Set", IsGenericMethod: true } &&
												  method.GetParameters().Length == 0);
		var set = setMethod.MakeGenericMethod(companyType).Invoke(dbContext, null)!;
		set.GetType().GetMethod("Add")!.Invoke(set, [company]);
		var save = dbContext.GetType().GetMethods().First(method => method.Name == "SaveEntitiesAsync");
		await (Task)save.Invoke(dbContext, [cancellationToken])!;

		return (Guid)company.GetType().GetProperty("Id")!.GetValue(company)!;
	}

	private static object ResolveAppDbContext(IServiceProvider services, string? assemblyPrefix)
	{
		var type = FindType("AppDbContext",
							namespaceFragment: null,
							assemblyPrefix);
		return services.GetRequiredService(type);
	}

	private static Type FindType(string typeName,
								 string? namespaceFragment,
								 string? assemblyPrefix)
	{
		var matches = AppDomain.CurrentDomain
							   .GetAssemblies()
							   .SelectMany(SafeTypes)
							   .Where(type => type.Name == typeName);
		if (!string.IsNullOrEmpty(namespaceFragment))
			matches = matches.Where(type => type.Namespace?.Contains(namespaceFragment, StringComparison.Ordinal) ?? false);
		if (!string.IsNullOrEmpty(assemblyPrefix))
			matches = matches.Where(type => type.Namespace?.StartsWith(assemblyPrefix, StringComparison.Ordinal) ?? false);

		return matches.First();
	}

	private static IEnumerable<Type> SafeTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(type => type is not null)!;
		}
	}
}