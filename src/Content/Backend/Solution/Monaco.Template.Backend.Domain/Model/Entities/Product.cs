using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model.Entities;

public class Product : AggregateRoot
{
	public const int TitleLength = 100;
	public const int DescriptionLength = 500;

	protected Product()
	{
	}

	public Product(string title,
				   string description,
				   decimal price,
				   Company company,
				   List<Image> pictures,
				   Image defaultPicture)
	{
		Company = company;

		(Title, Description, Price) = Validate(title,
											   description,
											   price);
		pictures.ToList()
				.Throw()
				.IfEmpty();

		defaultPicture.Throw()
					  .IfFalse(pictures.Contains);

		pictures.ForEach(AddPicture);
		SetDefaultPicture(defaultPicture);
	}

	public string Title { get; private set; } = null!;
	public string Description { get; private set; } = null!;
	public decimal Price { get; private set; }

	public Guid CompanyId { get; private set; }
	public virtual Company Company { get; private set; } = null!;

	private readonly List<Image> _pictures = [];
	public virtual IReadOnlyList<Image> Pictures => _pictures;

	public Guid DefaultPictureId { get; private set; }
	public virtual Image DefaultPicture { get; private set; } = null!;

	public virtual void Update(string title,
							   string description,
							   decimal price,
							   Company company)
	{
		(Title, Description, Price) = Validate(title,
											   description,
											   price);
		Company = company;
	}

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

	public virtual void AddPicture(Image picture)
	{
		if (Pictures.Contains(picture))
			return;

		_pictures.Add(picture);
		picture.MakePermanent();
	}

	public virtual void RemovePicture(Image picture)
	{
		picture.Throw(() => new InvalidOperationException("Cannot delete the last picture. Product must always have a picture."))
			   .IfTrue(p => Pictures.Count == 1 && Pictures.Contains(p));

		if (!Pictures.Contains(picture))
			return;

		_pictures.Remove(picture);
		picture.MarkForRemoval();

		if (picture == DefaultPicture && Pictures.Any())
			DefaultPicture = Pictures[0];
	}

	public virtual void SetDefaultPicture(Image picture)
	{
		picture.Throw(() => new ArgumentOutOfRangeException(nameof(picture), "The picture must be part of the product."))
			   .IfNotContains(_ => Pictures, picture);

		DefaultPicture = picture;
	}
}