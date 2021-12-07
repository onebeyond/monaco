using DcslGs.Template.Common.Domain.Model;

namespace DcslGs.Template.Domain.Model;

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
        Name = name;
        Extension = extension;
        Size = size;
        ContentType = contentType;
        UploadedOn = DateTime.UtcNow;
        IsTemp = isTemp;
    }

    public string Name { get; protected set; }
    public string Extension { get; protected set; }
    public long Size { get; protected set; }
    public string ContentType { get; protected set; }
    public DateTime UploadedOn { get; protected set; }
    public bool IsTemp { get; protected set; }
    
    public void MakePermanent()
    {
        IsTemp = false;
    }
}