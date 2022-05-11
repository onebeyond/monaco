using DcslGs.Template.Common.Application.Commands;

namespace DcslGs.Template.Application.Features.File.Commands;

public class FileCreateCommand : CommandBase<Guid>
{
	public FileCreateCommand(Stream stream, string fileName, string contentType)
	{
		Stream = stream;
		FileName = fileName;
		ContentType = contentType;
	}

	public Stream Stream { get; }
	public string FileName { get; }
	public string ContentType { get; }
}