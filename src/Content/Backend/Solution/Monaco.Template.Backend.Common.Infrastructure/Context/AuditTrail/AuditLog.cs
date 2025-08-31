using Serilog;

namespace Monaco.Template.Backend.Common.Infrastructure.Context.AuditTrail;

public static class AuditLog
{
	public static void Audit(this ILogger logger, List<AuditEntry> auditEntries) =>
		logger.ForContext("AuditEntries", auditEntries, true)
			  .Information("Audit Trail");
}