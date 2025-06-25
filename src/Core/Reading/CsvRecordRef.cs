using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
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
        ReadOnlySpan<Meta> fieldMeta = slice.Fields.AsSpanUnsafe();

        _reader = slice.Reader;
        _data = ref MemoryMarshal.GetReference(slice.Data.Span);
        _meta = fieldMeta;
        FieldCount = fieldMeta.Length - 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(CsvReader<T> reader, ref T data, ReadOnlySpan<Meta> meta)
    {
        _reader = reader;
        _data = ref data;
        _meta = meta;
        FieldCount = meta.Length - 1;
    }

    /// <inheritdoc/>
    public int FieldCount
    {
        // storing this in a property simplifies looping over the fields a bit
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // separate local yields 158 bytes of code vs 163, it _might_ matter under some conditions and produces
            // [mov mov cmp jae] for loading the span and length, instead of [mov cmp jae mov] should also allow ILP
            // inlining NextStart eliminates one lea (total 3 bytes less)

            ReadOnlySpan<Meta> meta = _meta;
            return meta[index + 1].GetField(meta[index].NextStart, ref _data, _reader);
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
        int start = meta[index].NextStart;
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), meta[index + 1].End - start);
    }

    /// <summary>
    /// Data of the raw record, not including possible trailing newline.
    /// </summary>
    public ReadOnlySpan<T> RawValue
    {
        get
        {
            ReadOnlySpan<Meta> meta = _meta;
            int end = meta[^1].End; // JIT doesn't optimize the other bounds check unless the last index is accessed first
            int start = meta[0].NextStart;
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), end - start);
        }
    }

    /// <summary>
    /// Returns length of the raw record.
    /// </summary>
    /// <param name="includeTrailingNewline">Whether to include the length of the possible trailing newline</param>
    public int GetRecordLength(bool includeTrailingNewline = false)
    {
        return _meta.GetRecordLength(includeTrailingNewline);
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
            return $"{{ CsvRecordRef<{Token<T>.Name}>[{FieldCount}]: Uninitialized }}";
        }

        if (typeof(T) == typeof(byte))
        {
            return $"{{ CsvRecordRef<{Token<T>.Name}>[{FieldCount}]: \"{Encoding.UTF8.GetString(MemoryMarshal.AsBytes(RawValue))}\" }}";
        }

        return $"{{ CsvRecordRef<{Token<T>.Name}>[{FieldCount}]: \"{RawValue.ToString()}\" }}";
    }
}
