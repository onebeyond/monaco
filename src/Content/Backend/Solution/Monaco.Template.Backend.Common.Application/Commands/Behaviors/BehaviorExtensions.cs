using LinqKit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Monaco.Template.Backend.Common.Application.Commands.Behaviors;

public static class BehaviorExtensions
{
	private static readonly Type[]? CommandBaseDerivedTypes = null;
	private static readonly Type[]? CommandBaseOfResultDerivedTypes = null;

	/// <summary>
	/// Registers command validation behaviors for all command types derived from <c>CommandBase</c> or
	/// <c>CommandBase&lt;T&gt;</c> found in the specified assembly.
	/// </summary>
	/// <remarks>This method scans the provided assembly for types derived from <c>CommandBase</c> and
	/// <c>CommandBase&lt;T&gt;</c>. For each detected command type: <list type="bullet"> <item> <description>Scoped
	/// instances of <see cref="IPipelineBehavior{TRequest, TResponse}"/> are registered for validation existence checks
	/// using <c>CommandValidationExistsBehavior</c>.</description> </item> <item> <description>Scoped instances of <see
	/// cref="IPipelineBehavior{TRequest, TResponse}"/> are registered for validation logic using
	/// <c>CommandValidationBehavior</c>.</description> </item> </list></remarks>
	/// <param name="services">The <see cref="IServiceCollection"/> to which the validation behaviors will be added.</param>
	/// <param name="assembly">The assembly to scan for command types.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> with the registered validation behaviors.</returns>
	public static IServiceCollection RegisterCommandValidationBehaviors(this IServiceCollection services, Assembly assembly)
	{
		//Gets the CommandBase derived classes
		var commandBaseTypes = GetCommandBaseDerivedTypes(assembly);
		//And adds the corresponding scoped behaviors for all the detected commands (for both existance and validation checks)
		commandBaseTypes.ForEach(t => services.AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(CommandResult)),
														 typeof(CommandValidationExistsBehavior<>).MakeGenericType(t))
											  .AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(CommandResult)),
														 typeof(CommandValidationBehavior<>).MakeGenericType(t)));
		//Gets the CommandBases<T> derived classes
		var commandBaseResultTypes = GetCommandBaseOfResultDerivedTypes(assembly);

		//And adds the corresponding scoped behavior for all the detected commands (only for validation checks)
		commandBaseResultTypes.ForEach(t =>
									   {
										   var tResult = t.BaseType!.GenericTypeArguments.First();
										   services.AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(CommandResult<>).MakeGenericType(tResult)),
															  typeof(CommandValidationExistsBehavior<,>).MakeGenericType(t, tResult))
												   .AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(CommandResult<>).MakeGenericType(tResult)),
															  typeof(CommandValidationBehavior<,>).MakeGenericType(t, tResult));
									   });
		return services;
	}

	/// <summary>
	/// Registers pipeline behaviors to handle concurrency exceptions for command types in the specified assembly.
	/// </summary>
	/// <remarks>This method scans the provided assembly for types derived from <c>CommandBase</c> and
	/// <c>CommandBase&lt;TResult&gt;</c>. For each detected command type, it registers a corresponding scoped pipeline
	/// behavior to handle concurrency exceptions.</remarks>
	/// <param name="services">The <see cref="IServiceCollection"/> to which the behaviors will be added.</param>
	/// <param name="assembly">The assembly containing the command types to scan for concurrency exception behaviors.</param>
	/// <returns>The updated <see cref="IServiceCollection"/> with the registered behaviors.</returns>
	public static IServiceCollection RegisterCommandConcurrencyExceptionBehaviors(this IServiceCollection services, Assembly assembly)
	{
		//Gets the CommandBase derived classes
		var commandBaseTypes = GetCommandBaseDerivedTypes(assembly);
		//And adds the corresponding scoped behaviors for all the detected commands
		commandBaseTypes.ForEach(t => services.AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(CommandResult)),
														 typeof(ConcurrencyExceptionBehavior<>).MakeGenericType(t)));
		//Gets the CommandBases<T> derived classes
		var commandBaseResultTypes = GetCommandBaseOfResultDerivedTypes(assembly);

		//And adds the corresponding scoped behavior for all the detected commands
		commandBaseResultTypes.ForEach(t =>
									   {
										   var tResult = t.BaseType!.GenericTypeArguments.First();
										   services.AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(t, typeof(CommandResult<>).MakeGenericType(tResult)),
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