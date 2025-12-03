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
            ReadOnlySpan<uint> fields = Record.GetFields(Reader._recordBuffer);

            uint last = fields[^1];
            int start = Field.NextStart(fields[0]);

            if (Record.IsFirst)
            {
                start = 0;
            }

            return Data[start..Field.End(last)].Span;
        }
    }

    public int FieldCount => Record.FieldCount;

    public ReadOnlySpan<T> GetField(int index, bool raw = false)
    {
        ReadOnlySpan<T> data = Data.Span;
        ReadOnlySpan<uint> fields = Record.GetFields(Reader._recordBuffer);
        int start = Field.NextStart(fields[index]);
        int end = Field.End(fields[index + 1]);

        if (Record.IsFirst && index == 0)
        {
            start = 0;
        }

        if (raw)
        {
            return data[start..end];
        }

        return Field.GetValue(
            start,
            end,
            Record.GetQuotes(Reader._recordBuffer)[index + 1],
            ref MemoryMarshal.GetReference(data),
            Reader
        );
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
