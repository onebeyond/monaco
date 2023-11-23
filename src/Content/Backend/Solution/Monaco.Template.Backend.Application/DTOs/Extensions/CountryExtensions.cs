using Monaco.Template.Backend.Domain.Model;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Application.DTOs.Extensions;

public static class CountryExtensions
{
	public static CountryDto? Map(this Country? value) =>
		value is null
			? null
			: new(value.Id,
				  value.Name);

	public static Dictionary<string, Expression<Func<Country, object>>> GetMappedFields() =>
		new()
		{
			[nameof(CountryDto.Id)] = x => x.Id,
			[nameof(CountryDto.Name)] = x => x.Name
		};
}