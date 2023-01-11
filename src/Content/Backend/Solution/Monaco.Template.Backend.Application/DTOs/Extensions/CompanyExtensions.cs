using Monaco.Template.Backend.Application.DTOs.Extensions;
using Monaco.Template.Backend.Application.Features.Company.Commands;
using Monaco.Template.Backend.Domain.Model;
using System.Linq.Expressions;

namespace Monaco.Template.Backend.Application.DTOs.Extensions;

public static class CompanyExtensions
{
	public static CompanyDto? Map(this Company? value, bool expandCountry = false) =>
		value is null
			? null
			: new()
			{
				Id = value.Id,
				Name = value.Name,
				Email = value.Email,
				WebSiteUrl = value.WebSiteUrl,
				Street = value.Address?.Street,
				City = value.Address?.City,
				County = value.Address?.County,
				PostCode = value.Address?.PostCode,
				CountryId = value.Address?.CountryId,
				Country = expandCountry ? value.Address?.Country.Map() : null
			};

	public static Company Map(this CompanyCreateCommand value, Country? country) =>
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

	public static void Map(this CompanyEditCommand value, Company item, Country? country) =>
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

	public static Dictionary<string, Expression<Func<Company, object>>> GetMappedFields() =>
		new()
		{
			[nameof(CompanyDto.Id)] = x => x.Id,
			[nameof(CompanyDto.Name)] = x => x.Name,
			[nameof(CompanyDto.Email)] = x => x.Email,
			[nameof(CompanyDto.WebSiteUrl)] = x => x.WebSiteUrl,
			[nameof(CompanyDto.Street)] = x => x.Address!.Street!,
			[nameof(CompanyDto.City)] = x => x.Address!.City!,
			[nameof(CompanyDto.County)] = x => x.Address!.County!,
			[nameof(CompanyDto.PostCode)] = x => x.Address!.PostCode!,
			[nameof(CompanyDto.CountryId)] = x => x.Address!.CountryId,
			[$"{nameof(CompanyDto.Country)}.{nameof(CountryDto.Name)}"] = x => x.Address!.Country.Name
		};
}