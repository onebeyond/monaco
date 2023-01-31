using Monaco.Template.Backend.Common.Domain.Model;
using Monaco.Template.Backend.Common.Domain.Model.Contracts;

namespace Monaco.Template.Backend.Domain.Model;

public class Country : Entity, IReferential
{
	protected Country() { }

	public Country(string name)
	{
		Name = name;
	}

	public string Name { get; private set; }
}