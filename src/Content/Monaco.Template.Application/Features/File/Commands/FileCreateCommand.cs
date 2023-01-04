using Monaco.Template.Common.Application.Commands;

namespace Monaco.Template.Application.Features.File.Commands;

public record FileCreateCommand(Stream Stream, string FileName, string ContentType) : CommandBase<Guid>;