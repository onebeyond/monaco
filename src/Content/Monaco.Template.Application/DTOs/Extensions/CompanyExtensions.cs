using System.Linq.Expressions;
using Monaco.Template.Domain.Model;
using Monaco.Template.Application.Features.Company.Commands;

namespace Monaco.Template.Application.DTOs.Extensions;

public static class CompanyExtensions
{
	public static CompanyDto? Map(this Company? value, bool expandCountry = false)
	{
		if (value == null)
			return null;

		return new()
			   {
				   Id = value.Id,
				   Name = value.Name,
				   Email = value.Email,
				   WebSiteUrl = value.WebSiteUrl,
				   Address = value.Address,
				   City = value.City,
				   County = value.County,
				   PostCode = value.PostCode,
				   CountryId = value.CountryId,
				   Country = expandCountry ? value.Country.Map() : null
			   };
	}

	public static Company Map(this CompanyCreateCommand value, Country country) =>
		new(value.Name,
			value.Email,
			value.WebSiteUrl,
			value.Address,
			value.City,
			value.County,
			value.PostCode,
			country);

	public static Dictionary<string, Expression<Func<Company, object>>> GetMappedFields() =>
		new()
		{
			[nameof(CompanyDto.Id)] = x => x.Id,
			[nameof(CompanyDto.Name)] = x => x.Name,
			[nameof(CompanyDto.Email)] = x => x.Email,
			[nameof(CompanyDto.WebSiteUrl)] = x => x.WebSiteUrl,
			[nameof(CompanyDto.Address)] = x => x.Address,
			[nameof(CompanyDto.City)] = x => x.City,
			[nameof(CompanyDto.County)] = x => x.County,
			[nameof(CompanyDto.PostCode)] = x => x.PostCode,
			[nameof(CompanyDto.CountryId)] = x => x.CountryId,
			[$"{nameof(CompanyDto.Country)}.{nameof(CountryDto.Name)}"] = x => x.Country.Name
		};
}