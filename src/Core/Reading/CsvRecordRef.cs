using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Internal implementation detail. This type should be used with care.
/// </summary>
[SkipLocalsInit]
[EditorBrowsable(EditorBrowsableState.Never)]
[PublicAPI]
public readonly ref struct CsvRecordRef<T>
    where T : unmanaged, IBinaryInteger<T>
{
    internal readonly ref T _data;
    internal readonly ReadOnlySpan<uint> _fields;
    internal readonly RecordOwner<T> _owner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(CsvReader<T> reader, RecordView view)
        : this(reader, ref MemoryMarshal.GetReference(reader._buffer.Span), view) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(RecordOwner<T> reader, ref T data, RecordView view)
    {
        view.AssertInvariants();

        _data = ref data;
        _owner = reader;
        _fields = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(reader._recordBuffer._fields), (uint)view.Start + 1),
            view.Length
        );
#if DEBUG
        _view = view;
    }

#pragma warning disable IDE0052
    private readonly RecordView _view;
#pragma warning restore IDE0052
#else
    }
#endif

    /// <summary>
    /// Gets the number of fields in the record.
    /// </summary>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fields.Length;
    }

    /// <summary>
    /// Returns the field at <paramref name="index"/>, unescaping and trimming it as per the current dialect settings.
    /// </summary>
    /// <param name="index">Zero-based index of the field to get.</param>
    /// <remarks>
    /// The returned span is only guaranteed to be valid until another field or the next record is read.
    /// </remarks>
    /// <exception cref="IndexOutOfRangeException"/>
    public ReadOnlySpan<T> this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // call indexer first to get bounds checks
            uint current = _fields[index];
            uint previous = Unsafe.Add(ref MemoryMarshal.GetReference(_fields), index - 1);

            uint end = current & Field.EndMask;
            uint start = (previous + 1) & Field.EndMask;

            int length = (int)(end - start);
            ref T startRef = ref Unsafe.Add(ref _data, start);

            // trimming check is 100% predictable
            if (_owner._dialect.Trimming != 0 || (int)(current << 2) < 0)
            {
                return Field.GetValue((int)start, current, ref _data, _owner);
            }

            Debug.Assert(end >= start, $"End index {end} is less than start index {start}");

            // if MSB (quoting) is not set, we don't need to mask the end at all
            return MemoryMarshal.CreateReadOnlySpan(ref startRef, length);
        }
    }

    /// <summary>
    /// Returns the field at <paramref name="index"/> <strong>without bounds checks</strong>,
    /// unescaping and trimming it as per the current dialect settings.
    /// </summary>
    /// <param name="index">Zero-based index of the field to get.</param>
    /// <remarks>
    /// The returned span is only guaranteed to be valid until another field or the next record is read.<br/>
    /// This method does not perform any bounds checking on <paramref name="index"/>, and is only intended to be used
    /// by internal code and the source generator.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetFieldUnsafe(int index)
    {
        // call indexer first to get bounds checks
        ref uint fieldRef = ref MemoryMarshal.GetReference(_fields);
        uint previous = Unsafe.Add(ref fieldRef, index - 1);
        uint current = Unsafe.Add(ref fieldRef, (uint)index);

        uint start = (previous + 1) & Field.EndMask;
        uint end = current & Field.EndMask;

        ref T startRef = ref Unsafe.Add(ref _data, start);
        int length = (int)(end - start);

        // trimming check is 100% predictable
        if (_owner._dialect.Trimming != 0 || (int)(current << 2) < 0)
        {
            return Field.GetValue((int)start, current, ref _data, _owner);
        }

        Debug.Assert(end >= start, $"End index {end} is less than start index {start}");
        return MemoryMarshal.CreateReadOnlySpan(ref startRef, length);
    }

    /// <summary>
    /// Returns the raw unescaped field at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Zero-based index of the field to get.</param>
    /// <remarks>
    /// The returned span is only guaranteed to be valid until another field or the next record is read.
    /// </remarks>
    /// <exception cref="IndexOutOfRangeException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRawSpan(int index)
    {
        // call indexer first to get bounds checks
        uint current = _fields[index];
        uint previous = Unsafe.Add(ref MemoryMarshal.GetReference(_fields), index - 1);

        uint end = current & Field.EndMask;
        uint start = (previous + 1) & Field.EndMask;

        int length = (int)(end - start);
        ref T startRef = ref Unsafe.Add(ref _data, start);

        Debug.Assert(end >= start, $"End index {end} is less than start index {start}");
        return MemoryMarshal.CreateReadOnlySpan(ref startRef, length);
    }

    /// <summary>
    /// Data of the raw record, not including possible trailing newline.
    /// </summary>
    public ReadOnlySpan<T> Raw
    {
        get
        {
            if (_fields.IsEmpty)
            {
                return [];
            }

            uint end = _fields[^1] & Field.EndMask;
            uint start = (1 + Unsafe.Add(ref Unsafe.AsRef(in _fields[0]), -1)) & Field.EndMask;

            Debug.Assert(end >= start, $"End index {end} is less than start index {start}");

            int length = (int)(end - start);
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), length);
        }
    }

    /// <summary>
    /// Returns length of the raw record, not including possible trailing newline.
    /// </summary>
    public int GetRecordLength()
    {
        if (_fields.IsEmpty)
        {
            return 0;
        }

        uint end = _fields[^1] & Field.EndMask;
        uint start = (uint)Field.NextStart(Unsafe.Add(ref Unsafe.AsRef(in _fields[0]), -1));

        Debug.Assert(end >= start, $"End index {end} is less than start index {start}");

        return (int)(end - start);
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

#if DEBUG
    static CsvRecordRef()
    {
        int size = Unsafe.SizeOf<CsvRecordRef<T>>();
        // Debug.Assert(size is 32, $"Unexpected size of CsvRecordRef<T>: {size}");
    }
#endif
}
