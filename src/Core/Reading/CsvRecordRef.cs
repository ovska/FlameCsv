using System.ComponentModel;
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
    internal CsvRecordRef(RecordOwner<T> owner, ref T data, RecordView view)
    {
        Check.False(owner.IsDisposed, "Cannot create CsvRecordRef from disposed reader.");
        view.AssertInvariants(owner._recordBuffer);

        _data = ref data;
        _owner = owner;
        _fields = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetReference(owner._recordBuffer._fields), (uint)view.Start + 1),
            view.Length
        );
    }

    /// <summary>
    /// Gets the number of fields in the record.
    /// </summary>
    /// <remarks>
    /// For a valid record, this is never zero.
    /// </remarks>
    public int FieldCount
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fields.Length;
    }

    /// <summary>
    /// Returns the 1-based line number of the record in the source data.
    /// </summary>
    /// <remarks>
    /// Lines are counted as CSV records; newlines inside quoted fields are not counted.
    /// </remarks>
    public int LineNumber
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner._recordBuffer.LineNumber;
    }

    /// <summary>
    /// Returns the 0-based position of the start of the first field of the record in the source data.
    /// </summary>
    /// <remarks>
    /// If <typeparamref name="T"/> is <c>char</c>, this is the character position, even if the data is from a byte stream.
    /// </remarks>
    public long Position
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _owner._recordBuffer.GetPosition(_fields);
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
            Check.False(_owner.IsDisposed, $"CsvRecordRef's owner is disposed ({_owner})");

            // call indexer first to get bounds checks
            uint current = _fields[index];
            uint previous = Unsafe.Add(ref MemoryMarshal.GetReference(_fields), index - 1);

            uint end = current & Field.EndMask;
            uint start = (previous + 1) & Field.EndMask;

            int length = (int)(end - start);
            ref T startRef = ref Unsafe.Add(ref _data, start);

            // trimming check is 100% predictable
            if (_owner.Trimming != 0 || Field.IsQuoted(current))
            {
                return Field.GetValue((int)start, current, ref _data, _owner);
            }

            Check.GreaterThanOrEqual(end, start, "Malformed fields");

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
    /// by internal code and the source generator, or after validating <see cref="FieldCount"/>.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetFieldUnsafe(int index)
    {
        Check.LessThanOrEqual((uint)index, (uint)_fields.Length, "Index out of range");
        Check.False(_owner.IsDisposed, $"CsvRecordRef's owner is disposed ({_owner})");

        ref uint fieldRef = ref MemoryMarshal.GetReference(_fields);
        uint previous = Unsafe.Add(ref fieldRef, index - 1);
        uint current = Unsafe.Add(ref fieldRef, (uint)index);

        uint start = (previous + 1) & Field.EndMask;
        uint end = current & Field.EndMask;

        ref T startRef = ref Unsafe.Add(ref _data, start);
        int length = (int)(end - start);

        // trimming check is 100% predictable
        if (_owner.Trimming != 0 || Field.IsQuoted(current))
        {
            return Field.GetValue((int)start, current, ref _data, _owner);
        }

        Check.GreaterThanOrEqual(end, start, "Malformed fields");
        return MemoryMarshal.CreateReadOnlySpan(ref startRef, length);
    }

    /// <summary>
    /// Returns the raw unescaped field at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Zero-based index of the field to get.</param>
    /// <remarks>
    /// The returned span is only guaranteed to be valid until the next record is read.
    /// </remarks>
    /// <exception cref="IndexOutOfRangeException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> GetRawSpan(int index)
    {
        Check.False(_owner.IsDisposed, $"CsvRecordRef's owner is disposed ({_owner})");

        // call indexer first to get bounds checks
        uint current = _fields[index];
        uint previous = Unsafe.Add(ref MemoryMarshal.GetReference(_fields), index - 1);

        uint end = current & Field.EndMask;
        uint start = (previous + 1) & Field.EndMask;

        int length = (int)(end - start);
        ref T startRef = ref Unsafe.Add(ref _data, start);

        Check.GreaterThanOrEqual(end, start, "Malformed fields");
        return MemoryMarshal.CreateReadOnlySpan(ref startRef, length);
    }

    /// <summary>
    /// Validates quotes in the fields at the specified <paramref name="indexes"/>.
    /// </summary>
    /// <param name="indexes">Zero-based indexes of the fields to validate.</param>
    /// <remarks>
    /// This method does not perform any bounds checking on <paramref name="indexes"/>, and is only intended to be used
    /// by internal code and the source generator, or after validating <see cref="FieldCount"/>.
    /// </remarks>
    /// <exception cref="Exceptions.CsvFormatException"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ValidateFieldsUnsafe(params ReadOnlySpan<int> indexes)
    {
        Check.False(indexes.IsEmpty, "Don't call with empty indexes.");
        Check.False(_owner.AcceptInvalidQuotes, "Cannot validate fields when AcceptInvalidQuotes is enabled.");
        Check.False(_owner.IsDisposed, $"CsvRecordRef's owner is disposed ({_owner})");
        Check.NotEqual(_owner.Quote, default, "Cannot validate fields when Quote is not set.");
        Check.OneOf(
            _owner.Options.ValidateQuotes,
            [CsvQuoteValidation.ValidateUnreadFields, CsvQuoteValidation.ValidateAllRecords],
            "Invalid quote validation option."
        );

        ref uint fieldRef = ref MemoryMarshal.GetReference(_fields);

        foreach (int index in indexes)
        {
            uint current = Unsafe.Add(ref fieldRef, (uint)index);

            if (Field.IsQuoted(current))
            {
                uint previous = Unsafe.Add(ref fieldRef, index - 1);
                Field.EnsureValid(Field.NextStart(previous), current, ref _data, _owner);
            }
        }
    }

    /// <summary>
    /// Validates quotes in all fields.
    /// </summary>
    /// <exception cref="Exceptions.CsvFormatException"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void ValidateAllFields()
    {
        Check.False(_owner.IsDisposed, $"CsvRecordRef's owner is disposed ({_owner})");

        if (_owner.AcceptInvalidQuotes || _owner.Quote == default)
        {
            return;
        }

        ReadOnlySpan<uint> local = _fields;
        ref uint fieldRef = ref MemoryMarshal.GetReference(local);

        for (int i = 0; i < local.Length; i++)
        {
            uint current = local[i];

            if (Field.IsQuoted(current))
            {
                uint previous = Unsafe.Add(ref fieldRef, i - 1);
                Field.EnsureValid(Field.NextStart(previous), current, ref _data, _owner);
            }
        }
    }

    /// <summary>
    /// Data of the raw record, not including possible trailing newline.
    /// </summary>
    public ReadOnlySpan<T> Raw
    {
        get
        {
            Check.False(_owner.IsDisposed, $"CsvRecordRef's owner is disposed ({_owner})");

            if (_fields.IsEmpty) // guard against default(CsvRecordRef)
            {
                return [];
            }

            uint end = _fields[^1] & Field.EndMask;
            uint start = (1 + Unsafe.Add(ref Unsafe.AsRef(in _fields[0]), -1)) & Field.EndMask;

            Check.GreaterThanOrEqual(end, start, "Malformed fields");

            int length = (int)(end - start);
            return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref _data, start), length);
        }
    }

    /// <summary>
    /// Returns length of the raw record, not including possible trailing newline.
    /// </summary>
    public int GetRecordLength()
    {
        Check.False(_owner.IsDisposed, $"CsvRecordRef's owner is disposed ({_owner})");

        if (_fields.IsEmpty) // guard against default(CsvRecordRef)
        {
            return 0;
        }

        uint end = _fields[^1] & Field.EndMask;
        uint start = (uint)Field.NextStart(Unsafe.Add(ref Unsafe.AsRef(in _fields[0]), -1));

        Check.GreaterThanOrEqual(end, start, "Malformed fields");

        return (int)(end - start);
    }

    /// <summary>
    /// Returns metadata about the field at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Zero-based index of the field to get.</param>
    /// <returns>
    /// The raw length of the field, whether it has quotes, and whether it needs unescaping.
    /// </returns>
    public CsvFieldMetadata GetMetadata(int index)
    {
        Check.False(_owner.IsDisposed, $"CsvRecordRef's owner is disposed ({_owner})");

        uint current = _fields[index];
        uint previous = Unsafe.Add(ref MemoryMarshal.GetReference(_fields), index - 1);
        return new CsvFieldMetadata(current, Field.NextStart(previous));
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

        if (_owner.IsDisposed)
        {
            return $"{{ CsvRecordRef<{Token<T>.Name}>[{FieldCount}]: Disposed }}";
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
            if (record._owner.IsDisposed)
            {
                Raw = "<owner disposed>";
                Fields = [];
                return;
            }

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
        Check.Equal(Unsafe.SizeOf<CsvRecordRef<T>>(), 32);
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<T> GetFieldUnsafe(uint previous, uint current)
    {
        Check.False(_owner.IsDisposed, $"CsvRecordRef's owner is disposed ({_owner})");

        // trimming check is 100% predictable
        if (_owner.Trimming != 0 || Field.IsQuoted(current))
        {
            return Field.GetValue2(previous, current, ref _data, _owner);
        }

        uint start = (previous + 1) & Field.EndMask;
        uint end = current & Field.EndMask;

        ref T startRef = ref Unsafe.Add(ref _data, start);
        int length = (int)(end - start);

        Check.GreaterThanOrEqual(end, start, "Malformed fields");
        return MemoryMarshal.CreateReadOnlySpan(ref startRef, length);
    }
}
