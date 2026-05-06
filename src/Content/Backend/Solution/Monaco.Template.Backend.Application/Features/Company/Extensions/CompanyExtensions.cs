using Monaco.Template.Backend.Application.Features.Company.DTOs;
using Monaco.Template.Backend.Application.Features.Country.DTOs;
using Monaco.Template.Backend.Application.Features.Country.Extensions;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Application.Features.Company.Extensions;

internal static class CompanyExtensions
{
	extension(Domain.Model.Entities.Company value)
	{
		public CompanyDto Map(bool expandCountry = false) =>
			new(value.Id,
				value.Name,
				value.Email,
				value.WebSiteUrl,
				value.Address?.Street,
				value.Address?.City,
				value.Address?.County,
				value.Address?.PostCode,
				value.Address?.CountryId,
				expandCountry ? value.Address?.Country.Map() : null);
	}

	extension(CreateCompany.Command value)
	{
		public Domain.Model.Entities.Company Map(Domain.Model.Entities.Country? country) =>
			new(value.Name,
				value.Email,
				value.WebSiteUrl,
				country is not null
					? new(value.Street,
						  value.City,
						  value.County,
						  value.PostCode,
						  country)
					: null);
	}

	extension(EditCompany.Command value)
	{
		public void Map(Domain.Model.Entities.Company item, Domain.Model.Entities.Country? country) =>
			item.Update(value.Name,
						value.Email,
						value.WebSiteUrl,
						country is not null
							? new(value.Street,
								  value.City,
								  value.County,
								  value.PostCode,
								  country)
							: null);
	}

	public static Dictionary<string, Expression<Func<Domain.Model.Entities.Company, object>>> GetMappedFields() =>
		new()
		{
			[nameof(CompanyDto.Id)] = x => x.Id,
			[nameof(CompanyDto.Name)] = x => x.Name,
			[nameof(CompanyDto.Email)] = x => x.Email,
			[nameof(CompanyDto.WebSiteUrl)] = x => x.WebSiteUrl!,
			[nameof(CompanyDto.Street)] = x => x.Address!.Street!,
			[nameof(CompanyDto.City)] = x => x.Address!.City!,
			[nameof(CompanyDto.County)] = x => x.Address!.County!,
			[nameof(CompanyDto.PostCode)] = x => x.Address!.PostCode!,
			[nameof(CompanyDto.CountryId)] = x => x.Address!.CountryId,
			[$"{nameof(CompanyDto.Country)}.{nameof(CountryDto.Name)}"] = x => x.Address!.Country.Name
		};
}