using Monaco.Template.Backend.Common.Application.Commands;

namespace Monaco.Template.Backend.Application.Features.File.Commands;

public record FileCreateCommand(Stream Stream, string FileName, string ContentType) : CommandBase<Guid>;