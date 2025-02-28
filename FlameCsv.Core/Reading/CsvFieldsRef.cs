using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail. This type should probably not be used directly.
/// Using an unitialized instance leads to undefined behavior.
/// </summary>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly ref struct CsvFieldsRef<T> : ICsvFields<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ref readonly CsvDialect<T> _dialect;
    private readonly ref T _data;
    private readonly Span<T> _unescapeBuffer;
    private readonly Allocator<T> _allocator;
    private readonly ref Meta _firstMeta;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(scoped ref readonly CsvFields<T> fields, Span<T> unescapeBuffer)
    {
        CsvParser<T> parser = fields.Parser;
        ReadOnlySpan<Meta> fieldMeta = fields.Fields;

        _dialect = ref parser._dialect;
        _allocator = parser._unescapeAllocator;
        _data = ref MemoryMarshal.GetReference(fields.Data.Span);
        _firstMeta = ref MemoryMarshal.GetReference(fieldMeta);
        FieldCount = fieldMeta.Length - 1;
        _unescapeBuffer = unescapeBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(
        CsvParser<T> parser,
        ReadOnlySpan<T> data,
        ReadOnlySpan<Meta> fields,
        Span<T> unescapeBuffer)
    {
        _dialect = ref parser._dialect;
        _allocator = parser._unescapeAllocator;
        _data = ref MemoryMarshal.GetReference(data);
        _firstMeta = ref MemoryMarshal.GetReference(fields);
        FieldCount = fields.Length - 1;
        _unescapeBuffer = unescapeBuffer;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CsvFieldsRef{T}"/>.
    /// </summary>
    /// <param name="fields"></param>
    /// <param name="allocator"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(scoped ref readonly CsvFields<T> fields, Allocator<T> allocator)
    {
        if (fields.Parser is null) Throw.InvalidOp_DefaultStruct(typeof(CsvFields<T>));
        ArgumentNullException.ThrowIfNull(allocator);

        CsvParser<T> parser = fields.Parser;
        ReadOnlySpan<Meta> fieldMeta = fields.Fields;

        _dialect = ref parser._dialect;
        _allocator = allocator;
        _data = ref MemoryMarshal.GetReference(fields.Data.Span);
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

            return Unsafe
                .Add(ref _firstMeta, index + 1)
                .GetField(
                    dialect: in _dialect,
                    start: Unsafe.Add(ref _firstMeta, index).NextStart,
                    data: ref _data,
                    buffer: _unescapeBuffer,
                    allocator: _allocator);
        }
    }

    /// <inheritdoc cref="CsvFields{T}.Record"/>
    public ReadOnlySpan<T> Record
    {
        get
        {
            EnsureInitialized();

            int start = _firstMeta.NextStart;
            int end = Unsafe.Add(ref _firstMeta, FieldCount).End;

            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), end - start);
        }
    }

    /// <inheritdoc cref="CsvFields{T}.GetRecordLength"/>
    public int GetRecordLength(bool includeTrailingNewline = false)
    {
        EnsureInitialized();

        int start = _firstMeta.NextStart;
        Meta lastMeta = Unsafe.Add(ref _firstMeta, FieldCount);
        int end = includeTrailingNewline ? lastMeta.NextStart : lastMeta.End;
        return end - start;
    }

    /// <summary>
    /// Returns a diagnostic string representation of the current instance.
    /// </summary>
    /// <remarks>
    /// See <see cref="Record"/> to get the actual record span.
    /// </remarks>
    public override string ToString()
    {
        if (FieldCount == 0)
        {
            return $"{{ CsvFieldsRef<{Token<T>.Name}>: Uninitialized }}";
        }

        ReadOnlySpan<T> value = Record;

        if (typeof(T) == typeof(char))
        {
            return $"{{ CsvLine<{Token<T>.Name}>[{FieldCount}]: \"{MemoryMarshal.Cast<T, char>(Record)}\" }}";
        }

        if (typeof(T) == typeof(byte))
        {
            return
                $"{{ CsvLine<{Token<T>.Name}>[{FieldCount}]: \"{Encoding.UTF8.GetString(MemoryMarshal.Cast<T, byte>(Record))}\" }}";
        }

        return $"{{ CsvLine<{Token<T>.Name}>[{FieldCount}]: Length: {value.Length} }}";
    }

    private void EnsureInitialized()
    {
        if (FieldCount == 0 ||
            Unsafe.IsNullRef(in _dialect) ||
            Unsafe.IsNullRef(in _data) ||
            Unsafe.IsNullRef(in _firstMeta))
        {
            Throw.InvalidOp_DefaultStruct(typeof(CsvFieldsRef<T>));
        }
    }
}
