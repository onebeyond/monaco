using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model;

public class Product : Entity
{
	public const int TitleLength = 100;
	public const int DescriptionLength = 500;

	protected Product()
	{
	}

	public Product(string title,
				   string description,
				   decimal price) =>
		(Title, Description, Price) = Validate(title,
											   description,
											   price);

	public string Title { get; private set; }
	public string Description { get; private set; }
	public decimal Price { get; private set; }

	public Guid CompanyId { get; }
	public virtual Company Company { get; }

	private readonly List<Image> _pictures = [];
	public virtual IReadOnlyList<Image> Pictures => _pictures;

	public Guid DefaultPictureId { get; }
	public virtual Image DefaultPicture { get; private set; }

	public virtual void Update(string title,
							   string description,
							   decimal price) =>
		(Title, Description, Price) = Validate(title,
											   description,
											   price);

	private static (string title, string description, decimal price) Validate(string title,
																			  string description,
																			  decimal price) =>
		(title.Throw()
			  .IfEmpty()
			  .IfLongerThan(TitleLength),
		 description.Throw()
					.IfEmpty()
					.IfLongerThan(DescriptionLength),
		 price.Throw()
			  .IfNegative());

	public void AddPicture(Image picture, bool @default = false)
	{
		if (!Pictures.Contains(picture))
		{
			_pictures.Add(picture);
			picture.MakePermanent();
		}
		
		if (@default || _pictures.Count == 1)
			DefaultPicture = picture;
	}

	public void RemovePicture(Image picture)
	{
		if (Pictures.Contains(picture))
			_pictures.Remove(picture);

		if (picture == DefaultPicture && Pictures.Any())
			DefaultPicture = Pictures[0];
	}
}