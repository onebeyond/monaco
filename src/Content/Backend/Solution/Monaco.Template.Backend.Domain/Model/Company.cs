using Dawn;
using Monaco.Template.Backend.Common.Domain.Model;

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
		Name = Guard.Argument(name, nameof(name))
					.NotEmpty()
					.MaxLength(100);
		Email = Guard.Argument(email, nameof(email))
					 .NotEmpty();
		WebSiteUrl = webSiteUrl;
		Address = address;
	}

	public string Name { get; private set; }
	public string Email { get; private set; }
	public string WebSiteUrl { get; private set; }
	public byte[] Version { get; }

	public Address? Address { get; private set; }
	#if !excludeFilesSupport

	private readonly List<Product> _products = new();
	public virtual IReadOnlyList<Product> Products => _products;
	#endif
	
	public virtual void Update(string name,
							   string email,
							   string webSiteUrl,
							   Address? address)
	{
		Name = name;
		Email = email;
		WebSiteUrl = webSiteUrl;
		Address = address;
	}
	#if !excludeFilesSupport

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