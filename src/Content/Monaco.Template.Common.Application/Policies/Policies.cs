using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Registry;

namespace Monaco.Template.Common.Application.Policies;

public class Policies
{
	protected readonly PolicyRegistry PolicyRegistry;

	public const string DbConcurrentExceptionPolicyKey = "DbConcurrentExceptionPolicy";

	public Policies()
	{
		PolicyRegistry = new PolicyRegistry();
		PoliciesRegistration();
	}

	/// <summary>
	/// Performs the registration of the policies inside the policy registry
	/// </summary>
	protected virtual void PoliciesRegistration()
	{
		PolicyRegistry.Add(DbConcurrentExceptionPolicyKey,
						   Policy.Handle<DbUpdateConcurrencyException>()
								 .WaitAndRetry(3, i => TimeSpan.FromSeconds(i)));
		
		//To declare common policies
	}

	/// <summary>
	/// Returns the Policy Registry that contains the declaration of all the policies
	/// </summary>
	/// <returns></returns>
	public PolicyRegistry GetPolicyRegistry() => PolicyRegistry;
}