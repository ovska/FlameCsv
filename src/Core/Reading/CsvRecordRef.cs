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
/// Internal implementation detail. This type should not be used directly.
/// </summary>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
[PublicAPI]
public readonly ref struct CsvRecordRef<T> : ICsvRecord<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly bool _isFirst;
    private readonly ref T _data;
    private readonly ReadOnlySpan<uint> _fields;
    private readonly ref byte _quotes;
    internal readonly CsvReader<T> _reader;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(scoped ref readonly CsvSlice<T> slice)
        : this(slice.Reader, ref MemoryMarshal.GetReference(slice.Data.Span), slice.Record) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(CsvReader<T> reader, ref T data, RecordView view)
    {
        _isFirst = view.IsFirst;
        _reader = reader;
        _data = ref data;

        uint[] fields = view._fields;
        byte[] quotes = view._quotes;
        int start = view.Start + 1;
        int length = view.Count - 1;

        // skip the first which points to the start of the record

        _fields = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(fields), start),
            length
        );

        _quotes = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(quotes), start);
    }

    /// <inheritdoc/>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fields.Length;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // always access this first to ensure index is within bounds
            ref readonly uint current = ref _fields[index];
            byte quote = Unsafe.Add(ref _quotes, (uint)index);

            // very important to access the previous field in this manner for the CPU to optimize it with offset access
            int start = Field.NextStart(Unsafe.Add(ref Unsafe.AsRef(in current), -1));
            int end = Field.End(current);

            // very branch predictor friendly
            if (_isFirst && index == 0)
            {
                start = 0;
            }

            Debug.Assert(end >= start, $"End index {end} is less than start index {start}");

            int length = end - start;

            if ((((int)_reader._dialect.Trimming) | quote) != 0)
            {
                return Field.GetValue(start, current, quote, ref _data, _reader);
            }

            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, (uint)start), length);
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
        // always access this first to ensure index is within bounds
        ref readonly uint current = ref _fields[index];
        int start = Field.NextStart(Unsafe.Add(ref Unsafe.AsRef(in current), -1));
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, (uint)start), Field.End(current) - start);
    }

    /// <summary>
    /// Data of the raw record, not including possible trailing newline.
    /// </summary>
    public ReadOnlySpan<T> Raw
    {
        get
        {
            ReadOnlySpan<uint> fields = _fields;
            int end = Field.End(fields[^1]); // ensures the span is not empty
            int start = _isFirst ? 0 : Field.NextStart(Unsafe.Add(ref MemoryMarshal.GetReference(fields), -1));
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), end - start);
        }
    }

    /// <summary>
    /// Returns length of the raw record, not including possible trailing newline.
    /// </summary>
    public int GetRecordLength()
    {
        ReadOnlySpan<uint> fields = _fields;

        // ensure default(CsvRecordRef<T>) is handled correctly
        if (fields.IsEmpty)
        {
            return 0;
        }

        ref uint firstField = ref MemoryMarshal.GetReference(fields);

        return ReadExtensions.GetRecordLength(
            Unsafe.Add(ref firstField, -1),
            Unsafe.Add(ref firstField, fields.Length - 1),
            _isFirst,
            includeTrailingNewline: false
        );
    }

    /// <summary>
    /// Returns a diagnostic string representation of the current instance.
    /// </summary>
    /// <remarks>
    /// See <see cref="Raw"/> to get the actual record span.
    /// </remarks>
    public override string ToString()
    {
        if (FieldCount == 0)
        {
            return $"{{ CsvRecordRef<{Token<T>.Name}>[0]: Uninitialized }}";
        }

        if (typeof(T) == typeof(byte))
        {
            return $"{{ CsvRecordRef<{Token<T>.Name}>[{FieldCount}]: \"{Encoding.UTF8.GetString(MemoryMarshal.AsBytes(Raw))}\" }}";
        }

        return $"{{ CsvRecordRef<{Token<T>.Name}>[{FieldCount}]: \"{Raw.ToString()}\" }}";
    }
}
