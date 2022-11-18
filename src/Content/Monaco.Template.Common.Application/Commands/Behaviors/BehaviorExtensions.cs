using LinqKit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Monaco.Template.Common.Application.Commands.Contracts;
using System.Reflection;

namespace Monaco.Template.Common.Application.Commands.Behaviors;

public static class BehaviorExtensions
{
	private static readonly Type[]? CommandBaseDerivedTypes = null;
	private static readonly Type[]? CommandBaseOfResultDerivedTypes = null;

	public static IServiceCollection RegisterCommandValidationBehaviors(this IServiceCollection services, Assembly assembly)
    {
        //Gets the CommandBase derived classes
        var commandBaseTypes = GetCommandBaseDerivedTypes(assembly);
        //And adds the corresponding scoped behaviors for all the detected commands (for both existing and validation checks)
        commandBaseTypes.ForEach(t => services.AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(ICommandResult)),
                                                         typeof(CommandValidationExistsBehavior<>).MakeGenericType(t))
                                              .AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(ICommandResult)),
                                                         typeof(CommandValidationBehavior<>).MakeGenericType(t)));
        //Gets the CommandBases<T> derived classes
        var commandBaseResultTypes = GetCommandBaseOfResultDerivedTypes(assembly);

        //And adds the corresponding scoped behavior for all the detected commands (only for validation checks)
        commandBaseResultTypes.ForEach(t =>
                                       {
                                           var tResult = t.BaseType!.GenericTypeArguments.First();
                                           services.AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(ICommandResult<>).MakeGenericType(tResult)),
                                                              typeof(CommandValidationExistsBehavior<,>).MakeGenericType(t, tResult))
                                                   .AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(ICommandResult<>).MakeGenericType(tResult)),
                                                              typeof(CommandValidationBehavior<,>).MakeGenericType(t, tResult));
                                       });
        return services;
    }

	public static IServiceCollection RegisterCommandConcurrencyExceptionBehaviors(this IServiceCollection services, Assembly assembly)
	{
		//Gets the CommandBase derived classes
		var commandBaseTypes = GetCommandBaseDerivedTypes(assembly);
		//And adds the corresponding scoped behaviors for all the detected commands
		commandBaseTypes.ForEach(t => services.AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(ICommandResult)),
														 typeof(ConcurrencyExceptionBehavior<>).MakeGenericType(t)));
		//Gets the CommandBases<T> derived classes
		var commandBaseResultTypes = GetCommandBaseOfResultDerivedTypes(assembly);

		//And adds the corresponding scoped behavior for all the detected commands
		commandBaseResultTypes.ForEach(t =>
									   {
										   var tResult = t.BaseType!.GenericTypeArguments.First();
										   services.AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(ICommandResult<>).MakeGenericType(tResult)),
															  typeof(ConcurrencyExceptionBehavior<,>).MakeGenericType(t, tResult));
									   });
		return services;
	}


	private static Type[] GetCommandBaseDerivedTypes(Assembly assembly) =>
		CommandBaseDerivedTypes ??
		assembly.GetTypes()
				.Where(x => x.BaseType == typeof(CommandBase))
				.ToArray();

	private static Type[] GetCommandBaseOfResultDerivedTypes(Assembly assembly) =>
		CommandBaseOfResultDerivedTypes ??
		assembly.GetTypes()
				.Where(x => (x.BaseType?.IsGenericType ?? false) && x.BaseType?.GetGenericTypeDefinition() == typeof(CommandBase<>))
				.ToArray();
}