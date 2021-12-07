using System.Linq.Expressions;
using DcslGs.Template.Application.Commands.Company;
using DcslGs.Template.Domain.Model;

namespace DcslGs.Template.Application.DTOs.Extensions;

public static class CompanyExtensions
{
    public static CompanyDto? Map(this Company? value, bool expandCountry = false)
    {
        if (value == null)
            return null;

        return new CompanyDto
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

    public static CompanyCreateCommand MapCreateCommand(this CompanyCreateEditDto value)
    {
        return new CompanyCreateCommand(value.Name,
                                        value.Email,
                                        value.WebSiteUrl,
                                        value.Address,
                                        value.City, 
                                        value.County, 
                                        value.PostCode, 
                                        value.CountryId);
    }

    public static CompanyEditCommand MapEditCommand(this CompanyCreateEditDto value, Guid id)
    {
        return new CompanyEditCommand(id,
                                      value.Name,
                                      value.Email,
                                      value.WebSiteUrl,
                                      value.Address,
                                      value.City,
                                      value.County,
                                      value.PostCode,
                                      value.CountryId);
    }

    public static Company Map(this CompanyCreateCommand value, Country country)
    {
        return new Company(value.Name,
                           value.Email, 
                           value.WebSiteUrl,
                           value.Address,
                           value.City,
                           value.County, 
                           value.PostCode,
                           country);
    }

    public static Dictionary<string, Expression<Func<Company, object>>> GetMappedFields()
    {
        return new()
               {
                   { nameof(CompanyDto.Id), x => x.Id },
                   { nameof(CompanyDto.Name), x => x.Name },
                   { nameof(CompanyDto.Email), x => x.Email },
                   { nameof(CompanyDto.WebSiteUrl), x => x.WebSiteUrl },
                   { nameof(CompanyDto.Address), x => x.Address },
                   { nameof(CompanyDto.City), x => x.City },
                   { nameof(CompanyDto.County), x => x.County },
                   { nameof(CompanyDto.PostCode), x => x.PostCode },
                   { nameof(CompanyDto.CountryId), x => x.CountryId },
                   { $"{nameof(CompanyDto.Country)}.{nameof(CountryDto.Name)}", x => x.Country.Name }
               };
    }
}