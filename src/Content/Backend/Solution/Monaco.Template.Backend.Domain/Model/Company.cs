using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model;

public class Company : Entity
{
	protected Company()
	{
	}

	public Company(string name,
				   string email,
				   string webSiteUrl,
				   Address? address)
	{
		(Name, Email, WebSiteUrl) = Validate(name,
											 email,
											 webSiteUrl);
		Address = address;
	}

	public string Name { get; private set; }
	public string Email { get; private set; }
	public string WebSiteUrl { get; private set; }
	public byte[] Version { get; }

	public Address? Address { get; private set; }
	#if (filesSupport)

	private readonly List<Product> _products = [];
	public virtual IReadOnlyList<Product> Products => _products;
	#endif
	
	public virtual void Update(string name,
							   string email,
							   string webSiteUrl,
							   Address? address)
	{
		(Name, Email, WebSiteUrl) = Validate(name,
											 email,
											 webSiteUrl);
		Address = address;
	}

	private static (string name, string email, string webSiteUrl) Validate(string name,
																		   string email,
																		   string webSiteUrl) =>
		(name.Throw()
			 .IfEmpty()
			 .IfLongerThan(100),
		 email.Throw()
			  .IfEmpty()
			  .IfLongerThan(255),
		 webSiteUrl.Throw()
				   .IfLongerThan(300));
	#if (filesSupport)

	public void AddProduct(Product product)
	{
		if (!Products.Contains(product))
			_products.Add(product);
	}

	public void RemoveProduct(Product product)
	{
		if (Products.Contains(product))
			_products.Remove(product);
	}
	#endif
}