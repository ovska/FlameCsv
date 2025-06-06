using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;

namespace FlameCsv.Tests;

public readonly ref struct ConstantRecord : ICsvRecord<char>
{
    private readonly ReadOnlySpan<string> _values;

    public ConstantRecord([UnscopedRef] params ReadOnlySpan<string> values)
    {
        _values = values;
    }

    public ReadOnlySpan<char> this[int index] => _values[index];
    public int FieldCount => _values.Length;
}
