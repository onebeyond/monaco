using Monaco.Template.Backend.Application.Features.Country.DTOs;

namespace Monaco.Template.Backend.Application.Features.Country.Extensions;

public static class CountryExtensions
{
	extension(Domain.Model.Entities.Country value)
	{
		public CountryDto Map() =>
			new(value.Id, value.Name);
	}
}