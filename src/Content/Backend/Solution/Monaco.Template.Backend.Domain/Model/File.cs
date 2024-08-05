using Monaco.Template.Backend.Common.Domain.Model;
using Throw;

namespace Monaco.Template.Backend.Domain.Model;

public abstract class File : Entity
{
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
				   .IfLongerThan(300);
		Extension = extension.Throw()
							 .IfEmpty()
							 .IfLongerThan(20);
		Size = size.Throw()
				   .IfNegativeOrZero();
		ContentType = contentType.Throw()
								 .IfEmpty()
								 .IfLongerThan(50);
		UploadedOn = DateTime.UtcNow;
		IsTemp = isTemp;
	}

	public string Name { get; protected set; }
	public string Extension { get; protected set; }
	public long Size { get; protected set; }
	public string ContentType { get; protected set; }
	public DateTime UploadedOn { get; protected set; }
	public bool IsTemp { get; protected set; }

	public virtual void MakePermanent()
	{
		IsTemp = false;
	}
}