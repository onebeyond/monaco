using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model.Entities;

public abstract class File : AggregateRoot
{
	public const int NameLength = 300;
	public const int ExtensionLength = 20;
	public const int ContentTypeLength = 50;

	protected File()
	{
	}

	protected File(Guid id,
				   string name,
				   string extension,
				   long size,
				   string contentType,
				   bool isTemp) : base(id)
	{
		Name = name.Throw()
				   .IfEmpty()
				   .IfLongerThan(NameLength);
		Extension = extension.Throw()
							 .IfEmpty()
							 .IfLongerThan(ExtensionLength);
		Size = size.Throw()
				   .IfNegativeOrZero();
		ContentType = contentType.Throw()
								 .IfEmpty()
								 .IfLongerThan(ContentTypeLength);
		UploadedOn = DateTime.UtcNow;
		IsTemp = isTemp;
	}

	public string Name { get; protected set; } = null!;
	public string Extension { get; protected set; } = null!;
	public long Size { get; protected set; }
	public string ContentType { get; protected set; } = null!;
	public virtual DateTime UploadedOn { get; protected set; }
	public bool IsTemp { get; protected set; }

	public virtual void MakePermanent()
	{
		IsTemp = false;
	}

	public virtual void MarkForRemoval()
	{
		IsTemp = true;
	}
}