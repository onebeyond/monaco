namespace Monaco.Template.Domain.Model;

public class Document : File
{
    protected Document()
    {
    }

    public Document(Guid id,
                    string name,
                    string extension,
                    long size,
                    string contentType,
                    bool isTemp) : base(id, name, extension, size, contentType, isTemp)
    {
    }
}