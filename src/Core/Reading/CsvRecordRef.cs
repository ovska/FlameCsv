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
    internal readonly ReadOnlySpan<ulong> _bits;
    internal readonly RecordOwner<T> _owner;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(CsvReader<T> reader, RecordView view)
    {
        _data = ref MemoryMarshal.GetReference(reader._buffer.Span);
        _owner = reader;
        _bits = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(reader._recordBuffer._bits), (uint)view.Start),
            view.Length
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CsvRecordRef(RecordOwner<T> reader, ref T data, RecordView view)
    {
        _data = ref data;
        _owner = reader;
        _bits = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(reader._recordBuffer._bits), (uint)view.Start),
            view.Length
        );
    }

    /// <summary>
    /// Gets the number of fields in the record.
    /// </summary>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _bits.Length;
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
            ulong bits = _bits[index];

            if (_owner._dialect.Trimming != 0)
            {
                goto SlowPath;
            }

            uint start = (uint)bits;
            uint end = (uint)(bits >> 32);

            ref T startRef = ref Unsafe.Add(ref _data, start);
            int length = (int)(end - start);

            // trimming check is 100% predictable
            if ((long)bits < 0)
            {
                goto SlowPath;
            }

            Debug.Assert(end >= start, $"End index {end} is less than start index {start}");

            // if MSB (quoting) is not set, we don't need to mask the end at all
            return MemoryMarshal.CreateReadOnlySpan(ref startRef, length);

            SlowPath:
            return Field.GetValue(index, this);
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
        ulong bits = Unsafe.Add(ref MemoryMarshal.GetReference(_bits), (uint)index);

        if (_owner._dialect.Trimming != 0)
        {
            goto SlowPath;
        }

        uint start = (uint)bits;
        uint end = (uint)(bits >> 32);

        ref T startRef = ref Unsafe.Add(ref _data, start);
        int length = (int)(end - start);

        // trimming check is 100% predictable
        if ((long)bits < 0)
        {
            goto SlowPath;
        }

        Debug.Assert(end >= start, $"End index {end} is less than start index {start}");

        // if MSB (quoting) is not set, we don't need to mask the end at all
        return MemoryMarshal.CreateReadOnlySpan(ref startRef, length);

        SlowPath:
        return Field.GetValue(index, this);
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
        ref readonly ulong bits = ref _bits[index];

        uint start = (uint)bits;
        uint end = (uint)(bits >> 32) & Field.EndMask;
        Debug.Assert(end >= start, $"End index {end} is less than start index {start}");

        ref T startRef = ref Unsafe.Add(ref _data, start);
        int length = (int)(end - start);

        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), length);
    }

    /// <summary>
    /// Data of the raw record, not including possible trailing newline.
    /// </summary>
    public ReadOnlySpan<T> Raw
    {
        get
        {
            if (_bits.IsEmpty)
            {
                return [];
            }

            uint end = (uint)(_bits[^1] >> 32) & Field.EndMask;
            uint start = (uint)_bits[0];

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
        if (_bits.IsEmpty)
        {
            return 0;
        }

        uint end = (uint)(_bits[^1] >> 32) & Field.EndMask;
        uint start = (uint)_bits[0];

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
        Debug.Assert(size is 32, $"Unexpected size of CsvRecordRef<T>: {size}");
    }
#endif
}
