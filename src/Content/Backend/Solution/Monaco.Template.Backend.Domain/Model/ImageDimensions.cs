using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model;

public class ImageDimensions : ValueObject
{
	protected ImageDimensions()
	{ }

	public ImageDimensions(int height, int width)
	{
		Height = height.Throw()
					   .IfNegativeOrZero();
		Width = width.Throw()
					 .IfNegativeOrZero();
	}

	public int Height { get; }
	public int Width { get; }

	protected override IEnumerable<object?> GetEqualityComponents()
	{
		yield return Height;
		yield return Width;
	}
}