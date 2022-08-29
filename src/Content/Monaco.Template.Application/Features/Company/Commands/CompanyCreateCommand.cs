using Monaco.Template.Common.Application.Commands;

namespace Monaco.Template.Application.Features.Company.Commands;

public record CompanyCreateCommand : CommandBase<Guid>
{
    protected CompanyCreateCommand() { }

    public CompanyCreateCommand(string name,
                                string email,
                                string webSiteUrl,
                                string address,
                                string city,
                                string county,
                                string postCode,
                                Guid countryId)
    {
        Name = name;
        Email = email;
        WebSiteUrl = webSiteUrl;
        Address = address;
        City = city;
        County = county;
        PostCode = postCode;
        CountryId = countryId;
    }

    public string Name { get; }
    public string Email { get; }
    public string WebSiteUrl { get; }
    public string Address { get; }
    public string City { get; }
    public string County { get; }
    public string PostCode { get; }
    public Guid CountryId { get; }
}