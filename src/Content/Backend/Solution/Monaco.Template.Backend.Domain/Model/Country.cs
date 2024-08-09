using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Domain.Model.Contracts;
using Throw;

namespace Monaco.Template.Backend.Domain.Model;

public class Country : Entity, IReferential
{
	public const int NameLength = 100;

	protected Country() { }

	public Country(string name)
	{
		Name = name.Throw()
				   .IfEmpty()
				   .IfLongerThan(NameLength);
	}

	public string Name { get; private set; }
}