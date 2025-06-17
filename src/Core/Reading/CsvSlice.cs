using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

[DebuggerDisplay("{ToString(),nq}")]
[DebuggerTypeProxy(typeof(CsvSlice<>.CsvSliceDebugView))]
[SkipLocalsInit]
internal readonly struct CsvSlice<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public CsvReader<T> Reader { get; init; }
    public ReadOnlyMemory<T> Data { get; init; }
    public ArraySegment<Meta> Fields { get; init; }

    public ReadOnlySpan<T> RawValue
    {
        get
        {
            ReadOnlySpan<Meta> fields = Fields;
            return Data[fields[0].NextStart..fields[^1].End].Span;
        }
    }

    public int FieldCount => Fields.Count - 1;

    public ReadOnlySpan<T> GetField(int index, bool raw = false)
    {
        ReadOnlySpan<T> data = Data.Span;
        ReadOnlySpan<Meta> fields = Fields;
        int start = fields[index].NextStart;
        Meta meta = fields[index + 1];

        if (raw)
        {
            return data[start..meta.End];
        }

        return meta.GetField(start, ref MemoryMarshal.GetReference(data), Reader);
    }

    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        if (Fields.Count == 0)
        {
            return $"{{ CsvSlice<{Token<T>.Name}>[0] \"\" }}";
        }

        return $"{{ CsvSlice<{Token<T>.Name}>[{Fields.Count - 1}]: \"{Transcode.ToString(RawValue)}\" }}";
    }

    [ExcludeFromCodeCoverage]
    private class CsvSliceDebugView
    {
        public CsvSliceDebugView(CsvSlice<T> slice)
        {
            var reader = new CsvRecordRef<T>(in slice);

            Items = new string[reader.FieldCount];

            for (int i = 0; i < reader.FieldCount; i++)
            {
                Items[i] = Transcode.ToString(reader[i]);
            }
        }

        // ReSharper disable once CollectionNeverQueried.Local
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public string[] Items { get; }
    }
}
