namespace FlameCsv.Utilities;

internal sealed class TypeByteDictionary : TypeDictionary<string?, ReadOnlyMemory<byte>>
{
    public TypeByteDictionary(ISealable owner, TypeByteDictionary? source = null) : base(owner, source)
    {
    }

    protected override ReadOnlyMemory<byte> InitializeValue(string? value) => CsvDialectStatic.AsBytes(value);
}
