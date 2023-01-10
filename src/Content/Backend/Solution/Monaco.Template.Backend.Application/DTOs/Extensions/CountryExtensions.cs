using Monaco.Template.Domain.Model;
using System.Linq.Expressions;

namespace Monaco.Template.Application.DTOs.Extensions;

public static class CountryExtensions
{
	public static CountryDto? Map(this Country? value)
	{
		if (value == null)
			return null;

		var dto = new CountryDto
				  {
					  Id = value.Id,
					  Name = value.Name
				  };
		return dto;
	}

	public static Dictionary<string, Expression<Func<Country, object>>> GetMappedFields() =>
		new()
		{
			[nameof(CountryDto.Id)] = x => x.Id,
			[nameof(CountryDto.Name)] = x => x.Name
		};
}