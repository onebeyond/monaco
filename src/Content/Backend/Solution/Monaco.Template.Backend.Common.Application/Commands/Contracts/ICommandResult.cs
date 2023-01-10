using FluentValidation.Results;

namespace Monaco.Template.Common.Application.Commands.Contracts;

public interface ICommandResult<T> : ICommandResult
{
    T Result { get; set; }
}

public interface ICommandResult
{
    ValidationResult ValidationResult { get; set; }
    bool ItemNotFound { get; set; }
}