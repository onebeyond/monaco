using Polly.Bulkhead;

namespace Monaco.Template.Application.Policies;

public class Policies : Common.Application.Policies.Policies
{
	protected override void PoliciesRegistration()
	{
		base.PoliciesRegistration();

		//Add any other policies here or just remove the method if not used
	}
}