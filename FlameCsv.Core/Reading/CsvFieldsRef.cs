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
    private readonly ref Meta _firstMeta;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(scoped ref readonly CsvFields<T> fields, Span<T> unescapeBuffer)
    {
        CsvReader<T> reader = fields.Reader;
        ReadOnlySpan<Meta> fieldMeta = fields.Fields;

        _dialect = new Dialect<T>(reader.Options);
        _allocator = reader._unescapeAllocator;
        _data = ref MemoryMarshal.GetReference(fields.Data.Span);
        _firstMeta = ref MemoryMarshal.GetReference(fieldMeta);
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
        _firstMeta = ref fieldsRef;
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
            if ((uint)index >= (uint)FieldCount)
            {
                Debug.Assert(!Unsafe.IsNullRef(ref _firstMeta), "The struct was uninitialized");
                Throw.Argument_FieldIndex(index, FieldCount);
            }

            ref Meta meta = ref Unsafe.Add(ref _firstMeta, index + 1);
            int start = Unsafe.Add(ref _firstMeta, index).NextStart;
            return meta.GetField(_dialect, start, ref _data, _unescapeBuffer, _allocator);
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
        if ((uint)index >= (uint)FieldCount)
        {
            Debug.Assert(!Unsafe.IsNullRef(ref _firstMeta), "The struct was uninitialized");
            Throw.Argument_FieldIndex(index, FieldCount);
        }

        ref Meta meta = ref Unsafe.Add(ref _firstMeta, index + 1);
        int start = Unsafe.Add(ref _firstMeta, index).NextStart;

        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), meta.End - start);
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
        if ((uint)index >= (uint)FieldCount)
        {
            Debug.Assert(!Unsafe.IsNullRef(ref _firstMeta), "The struct was uninitialized");
            Throw.Argument_FieldIndex(index, FieldCount);
        }

        ref Meta meta = ref Unsafe.Add(ref _firstMeta, index + 1);
        int start = Unsafe.Add(ref _firstMeta, index).NextStart;
        return meta.GetField(_dialect, start, ref _data, _unescapeBuffer, allocator: null);
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

    internal ReadOnlySpan<Meta> GetMetaSpan()
    {
        EnsureInitialized();
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _firstMeta, 1), FieldCount);
    }

    private void EnsureInitialized()
    {
        if (
            FieldCount == 0
            || Unsafe.IsNullRef(in _dialect)
            || Unsafe.IsNullRef(in _data)
            || Unsafe.IsNullRef(in _firstMeta)
        )
        {
            Throw.InvalidOp_DefaultStruct(typeof(CsvFieldsRef<T>));
        }
    }
}
