using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail. This type should not be used directly.
/// </summary>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
[PublicAPI]
[DebuggerTypeProxy(typeof(CsvRecordRef<>.DebugProxy))]
public readonly ref struct CsvRecordRef<T> : ICsvRecord<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly ref T _data;
    private readonly ReadOnlySpan<int> _starts;
    private readonly ref int _ends;
    private readonly ref byte _quotes;
    internal readonly RecordOwner<T> _owner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(CsvReader<T> reader, RecordView view)
        : this(reader, reader._recordBuffer, ref MemoryMarshal.GetReference(reader._buffer.Span), view) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(RecordOwner<T> reader, RecordBuffer recordBuffer, ref T data, RecordView view)
    {
        _owner = reader;
        _data = ref data;

        _starts = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(recordBuffer._starts), view.Start),
            view.Count - 1
        );

        _ends = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(recordBuffer._ends), view.Start + 1);
        _quotes = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(recordBuffer._quotes), view.Start + 1);

#if DEBUG
#pragma warning disable IDE0052 // Remove unread private members
        _view = view;
        _recordBuffer = recordBuffer;
    }

    private readonly RecordView _view;
    private readonly RecordBuffer _recordBuffer;
#pragma warning restore IDE0052 // Remove unread private members
#else
    }
#endif

    /// <inheritdoc/>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _starts.Length;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // bounds-check w/ Span indexer's builtin throwhelper is faster than a custom ThrowHelper (less codegen)
            int start = _starts[index];
            int end = Unsafe.Add(ref _ends, (uint)index);
            byte quote = Unsafe.Add(ref _quotes, (uint)index);

            Debug.Assert(end >= start, $"End index {end} is less than start index {start}");

            ref T startRef = ref Unsafe.Add(ref _data, (uint)start);
            int length = end - start;

            if ((((int)_owner._dialect.Trimming) | quote) != 0)
            {
                return Field.GetValue(start, end, quote, ref _data, _owner);
            }

            return MemoryMarshal.CreateReadOnlySpan(ref startRef, length);
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
        // bounds-check w/ Span indexer's builtin throwhelper is faster than a custom ThrowHelper (less codegen)
        int start = _starts[index];
        int end = Unsafe.Add(ref _ends, (uint)index);
        int length = end - start;
        Debug.Assert(end >= start, $"End index {end} is less than start index {start}");
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, (uint)start), length);
    }

    /// <summary>
    /// Data of the raw record, not including possible trailing newline.
    /// </summary>
    public ReadOnlySpan<T> Raw
    {
        get
        {
            ReadOnlySpan<int> starts = _starts;

            if (starts.IsEmpty)
            {
                return [];
            }

            // bounds-check w/ Span indexer's builtin throwhelper is faster than a custom ThrowHelper (less codegen)
            int start = starts[0];
            int end = Unsafe.Add(ref _ends, (uint)starts.Length - 1u);
            int length = end - start;
            Debug.Assert(end >= start, $"End index {end} is less than start index {start}");
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, (uint)start), length);
        }
    }

    /// <summary>
    /// Returns length of the raw record, not including possible trailing newline.
    /// </summary>
    public int GetRecordLength()
    {
        ReadOnlySpan<int> starts = _starts;

        if (starts.IsEmpty)
        {
            return 0;
        }

        int start = starts[0];
        int end = Unsafe.Add(ref _ends, (uint)FieldCount - 1u);
        Debug.Assert(end >= start, $"End index {end} is less than start index {start}");
        return end - start;
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

    private class DebugProxy
    {
        public string Raw { get; }
        public string[] Fields { get; }

        public DebugProxy(CsvRecordRef<T> record)
        {
            Raw = Transcode.ToString(record.Raw);
            Fields = new string[record.FieldCount];

            for (int i = 0; i < record.FieldCount; i++)
            {
                Fields[i] = Transcode.ToString(record[i]);
            }
        }

        public override string ToString()
        {
            return $"{{ CsvRecordRef<{Token<T>.Name}>[{Fields.Length}] \"{Raw}\" }}";
        }
    }
}
