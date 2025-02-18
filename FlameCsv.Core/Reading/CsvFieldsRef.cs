using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail. This type should probably not be used directly.
/// </summary>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly ref struct CsvFieldsRef<T> : ICsvFields<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ref readonly CsvDialect<T> _dialect;
    private readonly int _newlineLength;
    private readonly ReadOnlySpan<T> _data;
    private readonly Span<T> _unescapeBuffer;
    private readonly Func<int, Span<T>> _getBuffer;
    private readonly ref Meta _firstMeta;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(scoped ref readonly CsvFields<T> fields, Span<T> unescapeBuffer)
    {
        CsvParser<T> parser = fields.Parser;
        ReadOnlySpan<Meta> fieldMeta = fields.Fields;

        _dialect = ref parser._dialect;
        _newlineLength = parser._newline.Length;
        _getBuffer = parser.GetUnescapeBuffer;
        _data = fields.Data.Span;
        _firstMeta = ref MemoryMarshal.GetReference(fieldMeta);
        FieldCount = fieldMeta.Length - 1;
        _unescapeBuffer = unescapeBuffer;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CsvFieldsRef{T}"/>.
    /// </summary>
    /// <param name="fields"></param>
    /// <param name="getBuffer"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvFieldsRef(scoped ref readonly CsvFields<T> fields, Func<int, Span<T>> getBuffer)
    {
        if (fields.Parser is null) Throw.InvalidOp_DefaultStruct(typeof(CsvFields<T>));
        ArgumentNullException.ThrowIfNull(getBuffer);

        CsvParser<T> parser = fields.Parser;
        ReadOnlySpan<Meta> fieldMeta = fields.Fields;

        _dialect = ref parser._dialect;
        _newlineLength = parser._newline.Length;
        _getBuffer = getBuffer;
        _data = fields.Data.Span;
        _firstMeta = ref Unsafe.AsRef(in fieldMeta[0]);
        FieldCount = fieldMeta.Length - 1;
        _unescapeBuffer = [];
    }

    /// <inheritdoc/>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    /// <inheritdoc/>
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
