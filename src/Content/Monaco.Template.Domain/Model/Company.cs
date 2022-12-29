using Dawn;
using Monaco.Template.Common.Domain.Model;

namespace Monaco.Template.Domain.Model;

public class Company : Entity
{
    protected Company()
    {
    }

    public Company(string name,
                   string email,
                   string webSiteUrl,
                   string address,
                   string city,
                   string county,
                   string postCode,
                   Country country)
    {
        Name = Guard.Argument(name, nameof(name))
					.NotEmpty()
					.MaxLength(100);
		Email = Guard.Argument(email, nameof(email))
					 .NotEmpty();
        WebSiteUrl = webSiteUrl;
        Address = address;
        City = city;
        County = county;
        PostCode = postCode;
        Country = country;
    }

    public string Name { get; private set; }
    public string Email { get; private set; }
    public string WebSiteUrl { get; private set; }
    public string Address { get; private set; }
    public string City { get; private set; }
    public string County { get; private set; }
    public string PostCode { get; private set; }
	public byte[] Version { get; }

    public Guid CountryId { get; private set; }
    public virtual Country Country { get; private set; }


    public virtual void Update(string name,
                               string email,
                               string webSiteUrl,
                               string address,
                               string city,
                               string county,
                               string postCode,
                               Country country)
    {
        Name = name;
        Email = email;
        WebSiteUrl = webSiteUrl;
        Address = address;
        City = city;
        County = county;
        PostCode = postCode;
        Country = country;
	}
}