using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model;

public class Address : ValueObject
{
	public const int StreetLength = 100;
	public const int CityLength = 100;
	public const int CountyLength = 100;
	public const int PostCodeLength = 10;

	protected Address()
	{ }

	public Address(string? street,
				   string? city,
				   string? county,
				   string? postCode,
				   Country country)
	{
		Street = street?.Throw()
					   .IfLongerThan(StreetLength);
		City = city?.Throw()
				   .IfLongerThan(CityLength);
		County = county?.Throw()
					   .IfLongerThan(CountyLength);
		PostCode = postCode?.Throw()
						   .IfLongerThan(PostCodeLength);
		Country = country.ThrowIfNull();
	}

	public string? Street { get; }
	public string? City { get; }
	public string? County { get; }
	public string? PostCode { get; }

	public Guid CountryId { get; }
	public virtual Country Country { get; }

	protected override IEnumerable<object?> GetEqualityComponents()
	{
		yield return Street;
		yield return City;
		yield return County;
		yield return PostCode;
		yield return Country;
	}
}