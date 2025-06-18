using Monaco.Template.Backend.Application.Features.Country.DTOs;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Application.Features.Country.Extensions;

public static class CountryExtensions
{
	public static CountryDto? Map(this Domain.Model.Entities.Country? value) =>
		value is null
			? null
			: new(value.Id,
				  value.Name);

	public static Dictionary<string, Expression<Func<Domain.Model.Entities.Country, object>>> GetMappedFields() =>
		new()
		{
			[nameof(CountryDto.Id)] = x => x.Id,
			[nameof(CountryDto.Name)] = x => x.Name
		};
}