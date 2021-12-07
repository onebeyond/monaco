using DcslGs.Template.Common.Application.Commands;

namespace DcslGs.Template.Application.Commands.File;

public class FileCreateCommand : CommandBase<Guid>
{
    public FileCreateCommand(Stream stream, string fileName, string contentType)
    {
        Stream = stream;
        FileName = fileName;
        ContentType = contentType;
    }

    public Stream Stream { get; private set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }
}