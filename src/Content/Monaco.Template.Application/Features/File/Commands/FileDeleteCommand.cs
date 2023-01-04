using Monaco.Template.Common.Application.Commands;

namespace Monaco.Template.Application.Features.File.Commands;

public record FileDeleteCommand(Guid Id) : CommandBase(Id);