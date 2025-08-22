namespace Monaco.Template.Backend.Common.Domain.Model.Contracts;

/// <summary>
/// Represents a marker interface used to indicate that an entity or object is not subject to auditing.
/// </summary>
/// <remarks>This interface is typically used in scenarios where certain entities should be excluded from audit
/// logging. Implementing this interface has no behavior by itself;  it serves as a semantic
/// indicator for systems that process or filter auditable entities.</remarks>
public interface INonAuditable;