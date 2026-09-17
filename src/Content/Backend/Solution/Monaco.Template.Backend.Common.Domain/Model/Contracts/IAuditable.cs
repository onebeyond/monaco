namespace Monaco.Template.Backend.Common.Domain.Model.Contracts;

/// <summary>
/// Transactional current-row creation and last-modification attribution for an opted-in entity.
/// </summary>
/// <remarks>
/// Implementing types expose who created or last modified the current row and when those writes occurred
/// in the same unit of work as the entity. This contract is not mutation history, event auditing,
/// deletion evidence, tamper evidence, or a compliance-grade audit trail. Types must implement it
/// explicitly; no base type or convention enrolls an entity.
/// </remarks>
public interface IAuditable
{
	const int ActorMaxLength = 512;

	DateTimeOffset CreatedAtUtc { get; }

	string? CreatedBy { get; }

	DateTimeOffset? ModifiedAtUtc { get; }

	string? ModifiedBy { get; }
}