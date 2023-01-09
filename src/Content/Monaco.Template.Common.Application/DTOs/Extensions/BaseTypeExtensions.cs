using Monaco.Template.Common.Domain.Model;
using Monaco.Template.Common.Domain.Model.Contracts;
using System.Linq.Expressions;

namespace Monaco.Template.Common.Application.DTOs.Extensions;

public static class BaseTypeExtensions
{
	public static TResult? Map<TEntity, TResult>(this TEntity? value) 
		where TEntity : Enumeration
		where TResult : BaseTypeDto, new()
	{
		if (value == null)
			return null;

		return new TResult
			   {
				   Id = value.Id,
				   Name = value.Name
			   };
	}


	public static Dictionary<string, Expression<Func<TEntity, object>>> GetMappedFields<TEntity>() where TEntity : Enumeration
	{
		return new()
			   {
				   { nameof(Enumeration.Id), x => x.Id },
				   { nameof(Enumeration.Name), x => x.Name }
			   };
	}
}