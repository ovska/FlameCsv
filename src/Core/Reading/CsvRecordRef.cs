using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail. This type should not be used directly.
/// </summary>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
[PublicAPI]
public readonly ref struct CsvRecordRef<T> : ICsvRecord<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ref T _data;
    private readonly ReadOnlySpan<Meta> _meta;
    internal readonly CsvReader<T> _reader;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(scoped ref readonly CsvSlice<T> slice)
    {
        _reader = slice.Reader;
        _data = ref MemoryMarshal.GetReference(slice.Data.Span);
        _meta = slice.Fields.AsSpanUnsafe(1); // skip the first which points to the start of the record
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(CsvReader<T> reader, ref T data, ReadOnlySpan<Meta> meta)
    {
        _reader = reader;
        _data = ref data;
        _meta = meta.Slice(1); // skip the first which points to the start of the record
    }

    /// <inheritdoc/>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _meta.Length;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // always access this first to ensure index is within bounds
            ref readonly Meta current = ref _meta[index];

            // very important to access the previous field in this manner for the CPU to optimize it with offset access
            int start = Unsafe.Add(ref Unsafe.AsRef(in current), -1).NextStart;

            if ((((int)_reader._dialect.Trimming) | (current._specialCountAndOffset & Meta.SpecialCountMask)) != 0)
            {
                return current.GetFieldSlow(start, ref _data, _reader);
            }

            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, (uint)start), current.End - start);
        }
    }

    /// <summary>
    /// Returns the raw unescaped span of the field at the specified index.
    /// </summary>
    /// <param name="index">0-based field index</param>
    /// <exception cref="IndexOutOfRangeException">
    /// Thrown if <paramref name="index"/> is less than 0 or greater than or equal to <see cref="FieldCount"/>
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRawSpan(int index)
    {
        ReadOnlySpan<Meta> meta = _meta;

        // takes care of bounds checks; meta is guaranteed to have 1 element at the head
        // always access this before the previous field to ensure we have the correct start
        Meta current = meta[index];

        int start = Unsafe.Add(ref MemoryMarshal.GetReference(meta), index - 1).NextStart;
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), current.End - start);
    }

    /// <summary>
    /// Data of the raw record, not including possible trailing newline.
    /// </summary>
    public ReadOnlySpan<T> RawValue
    {
        get
        {
            ReadOnlySpan<Meta> meta = _meta;
            int end = meta[^1].End;
            int start = Unsafe.Add(ref MemoryMarshal.GetReference(meta), -1).NextStart;
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), end - start);
        }
    }

    /// <summary>
    /// Returns length of the raw record.
    /// </summary>
    /// <param name="includeTrailingNewline">Whether to include the length of the possible trailing newline</param>
    public int GetRecordLength(bool includeTrailingNewline = false)
    {
        ReadOnlySpan<Meta> meta = _meta;

        // ensure default(CsvRecordRef<T>) is handled correctly
        if (meta.IsEmpty)
        {
            return 0;
        }

        return MemoryMarshal
            .CreateReadOnlySpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(meta), -1), meta.Length + 1)
            .GetRecordLength(includeTrailingNewline);
    }

    /// <summary>
    /// Returns a diagnostic string representation of the current instance.
    /// </summary>
    /// <remarks>
    /// See <see cref="RawValue"/> to get the actual record span.
    /// </remarks>
    public override string ToString()
    {
        if (FieldCount == 0)
        {
            return $"{{ CsvRecordRef<{Token<T>.Name}>[0]: Uninitialized }}";
        }

        if (typeof(T) == typeof(byte))
        {
            return $"{{ CsvRecordRef<{Token<T>.Name}>[{FieldCount}]: \"{Encoding.UTF8.GetString(MemoryMarshal.AsBytes(RawValue))}\" }}";
        }

        return $"{{ CsvRecordRef<{Token<T>.Name}>[{FieldCount}]: \"{RawValue.ToString()}\" }}";
    }
}
