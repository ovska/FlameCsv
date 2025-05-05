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
    private readonly ref T _data;
    private readonly CsvReader<T> _reader;
    private readonly ReadOnlySpan<Meta> _meta;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(scoped ref readonly CsvFields<T> fields)
    {
        CsvReader<T> reader = fields.Reader;
        ReadOnlySpan<Meta> fieldMeta = fields.Fields;

        _reader = fields.Reader;
        _data = ref MemoryMarshal.GetReference(fields.Data.Span);
        _meta = fieldMeta;
        FieldCount = fieldMeta.Length - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvFieldsRef(CsvReader<T> reader, ref T data, ReadOnlySpan<Meta> meta)
    {
        _reader = reader;
        _data = ref data;
        _meta = meta;
        FieldCount = meta.Length - 1;
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
            return _meta[index + 1].GetField(start, ref _data, _reader);
        }
    }

    /// <summary>
    /// Returns the raw unescaped span of the field at the specified index.
    /// </summary>
    /// <param name="index">0-based field index</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if <paramref name="index"/> is less than 0 or greater than or equal to <see cref="FieldCount"/>
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRawSpan(int index)
    {
        int start = _meta[index].NextStart;
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), _meta[index + 1].End - start);
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
