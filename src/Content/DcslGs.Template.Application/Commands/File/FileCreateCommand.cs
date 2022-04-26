using DcslGs.Template.Common.Application.Commands;

namespace DcslGs.Template.Application.Commands.File;

public record FileCreateCommand(Stream Stream, string FileName, string ContentType) : CommandBase<Guid>;