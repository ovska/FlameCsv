using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

[DebuggerDisplay("{ToString(),nq}")]
[DebuggerTypeProxy(typeof(CsvSlice<>.CsvSliceDebugView))]
[SkipLocalsInit]
internal readonly struct CsvSlice<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public CsvReader<T> Reader { get; init; }
    public ReadOnlyMemory<T> Data { get; init; }
    public RecordView Record { get; init; }

    public ReadOnlySpan<T> RawValue
    {
        get
        {
            (int start, int length) = Record.GetRecord(Reader._recordBuffer);
            return Data.Span.Slice(start, length);
        }
    }

    public int FieldCount => Record.FieldCount;

    public ReadOnlySpan<T> GetField(int index, bool raw = false)
    {
        ReadOnlySpan<T> data = Data.Span;
        (int start, int length) = Record.GetField(Reader._recordBuffer, index);
        byte quote = Record.GetQuote(Reader._recordBuffer, index);

        if (raw || quote == 0)
        {
            return data.Slice(start, length);
        }

        return Field.GetValue(start, start + length, quote, ref MemoryMarshal.GetReference(data), Reader);
    }

    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        if (Record.Count == 0)
        {
            return $"{{ CsvSlice<{Token<T>.Name}>[0] \"\" }}";
        }

        return $"{{ CsvSlice<{Token<T>.Name}>[{Record.FieldCount}]: \"{Transcode.ToString(RawValue)}\" }}";
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

        [UsedImplicitly]
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public string[] Items { get; }
    }
}
