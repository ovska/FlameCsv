namespace FlameCsv.Utilities;

internal sealed class TypeStringDictionary : TypeDictionary<string?, string?>
{
    public TypeStringDictionary(ISealable owner, TypeStringDictionary? source = null) : base(owner, source)
    {
    }

    protected override string? InitializeValue(string? value) => value;
}
