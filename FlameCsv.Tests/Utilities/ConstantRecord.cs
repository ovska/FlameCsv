using FlameCsv.Reading;

namespace FlameCsv.Tests.Utilities;

public readonly struct ConstantRecord<T> : ICsvRecordFields<T> where T : unmanaged, IBinaryInteger<T>
{
    public ConstantRecord(IEnumerable<string> values, CsvOptions<T>? options = null)
    {
        Values = values.Select(v => (options ?? CsvOptions<T>.Default).GetFromString(v)).ToArray();
    }

    public ReadOnlyMemory<T>[] Values { get; }
    public int FieldCount => Values.Length;
    public ReadOnlySpan<T> this[int index] => Values[index].Span;
}
