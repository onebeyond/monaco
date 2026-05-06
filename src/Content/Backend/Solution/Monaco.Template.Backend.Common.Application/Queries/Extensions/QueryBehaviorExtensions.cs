using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Monaco.Template.Backend.Common.Application.Queries.Behaviors;
using System.Reflection;
using Monaco.Template.Backend.Common.Application.Queries.Validators;

namespace Monaco.Template.Backend.Common.Application.Queries.Extensions;

public static class QueryBehaviorExtensions
{
	extension(IServiceCollection services)
	{
		/// <summary>
		/// Scans the assembly for query types returning <see cref="QueryResult{T}"/>, registers
		/// <see cref="QueryValidationBehavior{TQuery,TResult}"/> for each, and automatically registers
		/// the appropriate default base validators (<see cref="QueryPagedBaseValidator{T}"/> or
		/// <see cref="QueryPagedBaseValidator{T,TEntity}"/>).
		/// Specific validators already registered in DI will also be picked up by the behavior.
		/// </summary>
		public IServiceCollection RegisterQueryValidationBehaviors(Assembly assembly)
		{
			var queryTypes = assembly.GetTypes()
									 .Where(t => !t.IsAbstract &&
												 t.GetInterfaces().Any(i => i.IsGenericType &&
																			i.GetGenericTypeDefinition() == typeof(IRequest<>) &&
																			i.GenericTypeArguments[0].IsGenericType &&
																			i.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(QueryResult<>)));

			foreach (var queryType in queryTypes)
			{
				var resultType = queryType.GetInterfaces()
										  .First(i => i.IsGenericType &&
													  i.GetGenericTypeDefinition() == typeof(IRequest<>))
										  .GenericTypeArguments[0]  // QueryResult<T>
										  .GenericTypeArguments[0]; // T

				// Register the behavior
				services.AddScoped(typeof(IPipelineBehavior<,>).MakeGenericType(queryType, typeof(QueryResult<>).MakeGenericType(resultType)),
								   typeof(QueryValidationBehavior<,>).MakeGenericType(queryType, resultType));

				// Register default base validators
				RegisterDefaultValidators(services, queryType);
			}

			return services;
		}
	}

	private static void RegisterDefaultValidators(IServiceCollection services, Type queryType)
	{
		var validatorInterface = typeof(IValidator<>).MakeGenericType(queryType);
		var baseType = queryType.BaseType;

		while (baseType is not null && baseType != typeof(object))
		{
			if (baseType.IsGenericType)
			{
				var found = true;
				switch (baseType.GetGenericTypeDefinition())
				{
					case var t when t == typeof(QueryPagedBase<,>):
						services.AddScoped(validatorInterface, typeof(QueryPagedBaseValidator<,>).MakeGenericType(baseType.GenericTypeArguments));
						services.AddScoped(validatorInterface, typeof(QueryBaseValidator<,>).MakeGenericType(baseType.GenericTypeArguments));
						break;
					case var t when t == typeof(QueryPagedBase<>):
						services.AddScoped(validatorInterface, typeof(QueryPagedBaseValidator<>).MakeGenericType(baseType.GenericTypeArguments));
						break;
					case var t when t == typeof(QueryBase<,>):
						services.AddScoped(validatorInterface, typeof(QueryBaseValidator<,>).MakeGenericType(baseType.GenericTypeArguments));
						break;
					default:
						found = false;
						break;
				}

				if (found) break;
			}

			baseType = baseType.BaseType;
		}
	}
}