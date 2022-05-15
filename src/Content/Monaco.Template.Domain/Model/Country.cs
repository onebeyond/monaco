using Monaco.Template.Common.Domain.Model;
using Monaco.Template.Common.Domain.Model.Contracts;

namespace Monaco.Template.Domain.Model;

public class Country : Entity, IReferential
{
    protected Country() { }

    public Country(string name)
    {
        Name = name;
    }

    public string Name { get; private set; }
}