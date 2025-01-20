using FlameCsv.Reading;

namespace FlameCsv.Tests.Utilities;

public readonly struct ConstantRecord<T> : ICsvRecordFields<T> where T : unmanaged, IBinaryInteger<T>
{
    public ConstantRecord(IEnumerable<string> values, CsvOptions<T>? options = null)
    {
        Options = options ??= CsvOptions<T>.Default;
        Values = values.Select(v => options.GetFromString(v)).ToArray();
    }

    public CsvOptions<T> Options { get; }
    public ReadOnlyMemory<T>[] Values { get; }
    public int FieldCount => Values.Length;
    public ReadOnlySpan<T> this[int index] => Values[index].Span;
}
