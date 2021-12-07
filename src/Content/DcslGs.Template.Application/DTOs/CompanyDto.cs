namespace DcslGs.Template.Application.DTOs;

public class CompanyDto
{
    public Guid Id {  get; set; }
    public string Name { get; set; }
    public string JobCode { get; set; }
    public string ContactName { get; set; }
    public string PhoneNumber { get; set; }
    public string Email { get; set; }
    public string WebSiteUrl { get; set; }
    public DateTime ActiveFrom { get; set; }
    public string SageReferenceNumber { get; set; }
    public DateTime StartDate { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string County { get; set; }
    public string PostCode { get; set; }

    public Guid CountryId { get; set; }
    public CountryDto? Country { get; set; }
}