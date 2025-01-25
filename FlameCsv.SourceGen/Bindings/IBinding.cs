namespace FlameCsv.SourceGen.Bindings;

internal interface IBinding
{
    string Name { get; }
    IReadOnlyList<string> Names { get; }
    ISymbol Symbol { get; }
    ITypeSymbol Type { get; }
    bool IsRequired { get; }
    int Order { get; }
    CsvBindingScope Scope { get; }

    bool CanRead { get; }
    bool CanWrite { get; }

    void WriteConverterId(StringBuilder sb);
    void WriteHandlerId(StringBuilder sb);
    void WriteIndex(StringBuilder sb, int? index = null);
    int Index { get; }
}
