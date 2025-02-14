using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

[SkipLocalsInit]
[SuppressMessage("ReSharper", "ConvertToAutoPropertyWhenPossible")]
internal readonly ref struct MetaFieldReader<T> : ICsvRecordFields<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ref readonly CsvDialect<T> _dialect;
    private readonly int _newlineLength;
    private readonly ReadOnlySpan<T> _data;
    private readonly Span<T> _unescapeBuffer;
    private readonly CsvParser<T> _parser;

    private readonly ref Meta _firstMeta;
    private readonly int _fieldCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MetaFieldReader(
        ref readonly CsvLine<T> line,
        Span<T> unescapeBuffer = default)
    {
        _dialect = ref line.Parser._dialect;
        _newlineLength = line.Parser._newline.Length;
        _parser = line.Parser;
        _data = line.Data.Span;
        _firstMeta = ref MemoryMarshal.GetReference(line.Fields);
        _fieldCount = line.Fields.Length - 1;
        _unescapeBuffer = unescapeBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MetaFieldReader(
        CsvParser<T> parser,
        ReadOnlySpan<T> data,
        ReadOnlySpan<Meta> fields)
    {
        _dialect = ref parser._dialect;
        _newlineLength = parser._newline.Length;
        _parser = parser;
        _data = data;
        _firstMeta = ref MemoryMarshal.GetReference(fields);
        _fieldCount = fields.Length - 1;
        _unescapeBuffer = default;
    }

    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fieldCount;
    }

    public ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_fieldCount)
                Throw.Argument_FieldIndex(index, _fieldCount);

            ref Meta meta = ref Unsafe.Add(ref _firstMeta, index + 1);
            int start = Unsafe.Add(ref _firstMeta, index).GetNextStart(_newlineLength);

            return meta.GetField(
                dialect: in _dialect,
                start: start,
                data: _data,
                buffer: _unescapeBuffer,
                parser: _parser);
        }
    }
}
