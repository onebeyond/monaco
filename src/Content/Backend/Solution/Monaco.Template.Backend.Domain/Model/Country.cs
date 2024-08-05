using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Domain.Model.Contracts;
using Throw;

namespace Monaco.Template.Backend.Domain.Model;

public class Country : Entity, IReferential
{
	protected Country() { }

	public Country(string name)
	{
		Name = name.Throw()
				   .IfEmpty()
				   .IfLongerThan(100);
	}

	public string Name { get; private set; }
}