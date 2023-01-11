using Monaco.Template.Backend.Common.Application.Commands;

namespace Monaco.Template.Backend.Application.Features.Company.Commands;

public record CompanyDeleteCommand(Guid Id) : CommandBase(Id);