using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail. This type should probably not be used directly.
/// Using an unitialized instance leads to undefined behavior.
/// </summary>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
[PublicAPI]
public readonly ref struct CsvFieldsRef<T> : ICsvFields<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly Dialect<T> _dialect;
    private readonly ref T _data;
    private readonly Span<T> _unescapeBuffer;
    private readonly Allocator<T> _allocator;
    private readonly ReadOnlySpan<Meta> _meta;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(scoped ref readonly CsvFields<T> fields, Span<T> unescapeBuffer)
    {
        CsvReader<T> reader = fields.Reader;
        ReadOnlySpan<Meta> fieldMeta = fields.Fields;

        _dialect = new Dialect<T>(reader.Options);
        _allocator = reader._unescapeAllocator;
        _data = ref MemoryMarshal.GetReference(fields.Data.Span);
        _meta = fieldMeta;
        FieldCount = fieldMeta.Length - 1;
        _unescapeBuffer = unescapeBuffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(
        Dialect<T> dialect,
        Allocator<T> allocator,
        ref T data,
        ref Meta fieldsRef,
        int fieldsLength,
        Span<T> unescapeBuffer
    )
    {
        _dialect = dialect;
        _allocator = allocator;
        _data = ref data;
        _meta = MemoryMarshal.CreateReadOnlySpan(ref fieldsRef, fieldsLength);
        FieldCount = fieldsLength - 1;
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
        if (fields.Reader is null)
            Throw.InvalidOp_DefaultStruct(typeof(CsvFields<T>));
        ArgumentNullException.ThrowIfNull(allocator);

        CsvReader<T> reader = fields.Reader;
        ReadOnlySpan<Meta> fieldMeta = fields.Fields;

        _dialect = new Dialect<T>(reader.Options);
        _allocator = allocator;
        _data = ref MemoryMarshal.GetReference(fields.Data.Span);
        _meta = fieldMeta;
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
    public unsafe ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int start = _meta[index].NextStart;
            return _meta[index + 1].GetField(_dialect, start, ref _data, _unescapeBuffer, _allocator);
        }
    }

    /// <summary>
    /// Returns the raw unescaped span of the field at the specified index.
    /// </summary>
    /// <param name="index">0-based field index</param>
    /// <seealso cref="GetSpan(int, Span{T})"/>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="index"/> is less than 0 or greater than or equal to <see cref="FieldCount"/>
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRawSpan(int index)
    {
        int start = _meta[index].NextStart;
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), _meta[index + 1].End - start);
    }

    /// <summary>
    /// Returns the field span at the specified index.
    /// </summary>
    /// <param name="index">0-based field index</param>
    /// <param name="unescapeBuffer">Buffer to unescape the value to if needed</param>
    /// <remarks>
    /// The field is escaped to <paramref name="unescapeBuffer"/> if needed.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="index"/> is less than 0 or greater than or equal to <see cref="FieldCount"/>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="unescapeBuffer"/> is not large enough to hold the unescaped field.
    /// </exception>
    /// <seealso cref="GetRawSpan(int)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetSpan(int index, Span<T> unescapeBuffer)
    {
        int start = _meta[index].NextStart;
        return _meta[index + 1].GetField(_dialect, start, ref _data, unescapeBuffer, null);
    }

    /// <inheritdoc cref="CsvFields{T}.Record"/>
    public ReadOnlySpan<T> Record
    {
        get
        {
            int start = _meta[0].NextStart;
            int end = _meta[^1].End;
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), end - start);
        }
    }

    /// <inheritdoc cref="CsvFields{T}.GetRecordLength"/>
    public int GetRecordLength(bool includeTrailingNewline = false)
    {
        int start = _meta[0].NextStart;
        Meta lastMeta = _meta[^1];
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
            return $"{{ CsvFieldsRef<{Token<T>.Name}>[{FieldCount}]: Uninitialized }}";
        }

        if (typeof(T) == typeof(char))
        {
            return $"{{ CsvFieldsRef<{Token<T>.Name}>[{FieldCount}]: \"{MemoryMarshal.Cast<T, char>(Record)}\" }}";
        }

        if (typeof(T) == typeof(byte))
        {
            return $"{{ CsvFieldsRef<{Token<T>.Name}>[{FieldCount}]: \"{Encoding.UTF8.GetString(MemoryMarshal.AsBytes(Record))}\" }}";
        }

        return $"{{ CsvFieldsRef<{Token<T>.Name}>[{FieldCount}]: Length: {Record.Length} }}";
    }
}
