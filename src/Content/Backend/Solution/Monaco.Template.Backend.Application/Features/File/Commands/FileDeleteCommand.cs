using Monaco.Template.Backend.Common.Application.Commands;

namespace Monaco.Template.Backend.Application.Features.File.Commands;

public record FileDeleteCommand(Guid Id) : CommandBase(Id);