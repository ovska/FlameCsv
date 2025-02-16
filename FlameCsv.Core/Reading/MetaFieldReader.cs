using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

[SkipLocalsInit]
internal readonly ref struct MetaFieldReader<T> : ICsvRecordFields<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ref readonly CsvDialect<T> _dialect;
    private readonly int _newlineLength;
    private readonly ReadOnlySpan<T> _data;
    private readonly Span<T> _unescapeBuffer;
    private readonly Func<int, Span<T>> _getBuffer;
    private readonly ref Meta _firstMeta;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MetaFieldReader(scoped ref readonly CsvLine<T> line, Span<T> unescapeBuffer)
    {
        CsvParser<T> parser = line.Parser;
        ReadOnlySpan<Meta> fields = line.Fields;

        _dialect = ref parser._dialect;
        _newlineLength = parser._newline.Length;
        _getBuffer = parser.GetUnescapeBuffer;
        _data = line.Data.Span;
        _firstMeta = ref MemoryMarshal.GetReference(fields);
        FieldCount = fields.Length - 1;
        _unescapeBuffer = unescapeBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MetaFieldReader(scoped ref readonly CsvLine<T> line, Func<int, Span<T>> getBuffer)
    {
        CsvParser<T> parser = line.Parser;
        ReadOnlySpan<Meta> fields = line.Fields;

        _dialect = ref parser._dialect;
        _newlineLength = parser._newline.Length;
        _getBuffer = getBuffer;
        _data = line.Data.Span;
        _firstMeta = ref MemoryMarshal.GetReference(fields);
        FieldCount = fields.Length - 1;
        _unescapeBuffer = [];
    }

    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    public ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            Debug.Assert(!Unsafe.IsNullRef(ref _firstMeta), "MetaFieldReader was uninitialized");

            if ((uint)index >= (uint)FieldCount)
                Throw.Argument_FieldIndex(index, FieldCount);

            ref Meta meta = ref Unsafe.Add(ref _firstMeta, index + 1);
            int start = Unsafe.Add(ref _firstMeta, index).GetNextStart(_newlineLength);

            return meta.GetField(
                dialect: in _dialect,
                start: start,
                data: _data,
                buffer: _unescapeBuffer,
                getBuffer: _getBuffer);
        }
    }
}
