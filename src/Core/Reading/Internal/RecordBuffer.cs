using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;
using FlameCsv.Utilities;
using static FlameCsv.Reading.Internal.Field;

namespace FlameCsv.Reading.Internal;

[DebuggerTypeProxy(typeof(MetaBufferDebugView))]
[DebuggerDisplay("{ToString(),nq}")]
[SkipLocalsInit]
internal sealed class RecordBuffer : IDisposable
{
    public const int DefaultFieldBufferSize = 4096;

    /// <summary>
    /// Storage for the field metadata.
    /// </summary>
    internal uint[] _fields;

    /// <summary>
    /// Storage for quote counts.
    /// </summary>
    internal byte[] _quotes;

    /// <summary>
    /// Storage for EOL field indices.
    /// </summary>
    internal ushort[] _eols;

    internal int _eolIndex;
    internal int _eolCount;

    /// <summary>
    /// Number of fields that have been consumed from the buffer.
    /// </summary>
    private int _fieldIndex;

    /// <summary>
    /// Number of fields that have been parsed to the buffer.
    /// </summary>
    internal int _fieldCount;

    internal int[] _starts;
    internal int[] _ends;

    public RecordBuffer(int bufferSize = DefaultFieldBufferSize)
    {
        Initialize(bufferSize);
    }

    /// <summary>
    /// Returns unconsumed meta buffer. If 3 fields were left in the buffer on last reset,
    /// returns array length - 3.
    /// </summary>
    public FieldBuffer GetUnreadBuffer(int minimumLength, out int startIndex)
    {
        int start = _fieldCount + 1;

        // TODO: this might be a dead path?
        if ((_fields.Length - start) < minimumLength)
        {
            int newLength = Math.Max(_fields.Length * 2, minimumLength + start);
            ArrayPool<uint>.Shared.Resize(ref _fields, newLength);
            ArrayPool<byte>.Shared.Resize(ref _quotes, newLength);
            ArrayPool<ushort>.Shared.Resize(ref _eols, newLength);
            ArrayPool<int>.Shared.Resize(ref _starts, newLength);
            ArrayPool<int>.Shared.Resize(ref _ends, newLength);
        }

        startIndex = _fieldCount == 0 ? 0 : NextStart(_fields[_fieldCount]);
        return new()
        {
            //
            Fields = _fields.AsSpan(start),
            Quotes = _quotes.AsSpan(start),
        };
    }

    /// <summary>
    /// Number of fields that have been buffered and not yet consumed.
    /// </summary>
    public int UnreadFields
    {
        get
        {
            ObjectDisposedException.ThrowIf(_fields.Length == 0, this);
            return _fieldCount - _fieldIndex;
        }
    }

    /// <summary>
    /// End position of the last field that has been read.
    /// </summary>
    public int BufferedDataLength
    {
        get
        {
            ObjectDisposedException.ThrowIf(_fields.Length == 0, this);
            return NextStart(_fields[_fieldCount]);
        }
    }

    /// <summary>
    /// Number of complete records that have been buffered and not yet consumed.
    /// </summary>
    public int UnreadRecords => _eolCount - _eolIndex;

    /// <summary>
    /// Returns the end position of the last fully formed record that has been read, including trailing newline.
    /// </summary>
    public int BufferedRecordLength
    {
        get
        {
            ObjectDisposedException.ThrowIf(_fields.Length == 0, this);

            Debug.Assert(_fields[0] is 0, "First field should always be zero to indicate start of data");

            if (_eolCount == 0)
            {
                return 0;
            }

            uint lastField = _fields[_eols[_eolCount]];
            return NextStart(lastField);
        }
    }

    /// <summary>
    /// Marks fields as read, and returns the end position of the last field.
    /// </summary>
    /// <returns>
    /// Total number of fully formed records in the buffer.
    /// </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int SetFieldsRead(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        Debug.Assert(count >= 0);
        Debug.Assert((_fieldCount + count) < _fields.Length);

        _fieldCount += count;

        ref ushort eol = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_eols), (uint)_eolIndex + 1u);
        ref uint field = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_fields), (uint)_fieldIndex + 1u);
        ref int startRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_starts), (uint)_fieldIndex + 1u);
        ref int endRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_ends), (uint)_fieldIndex + 1u);

        nint end = _fieldCount - _fieldIndex;
        nint pos = 0;

        nuint idx = 0;

        // arm64
        if (AdvSimd.IsSupported)
        {
            nint unrolledEnd = end - Vector256<byte>.Count;
            Vector128<uint> endMask = Vector128.Create(EndMask);
            Vector128<uint> one = Vector128<uint>.One;

            while (pos <= unrolledEnd)
            {
                nuint width = (nuint)Vector128<uint>.Count;

                ref uint localField = ref Unsafe.Add(ref field, (nuint)pos);
                ref int localStart = ref Unsafe.Add(ref startRef, (nuint)pos);
                ref int localEnd = ref Unsafe.Add(ref endRef, (nuint)pos);

                // jagged order to improve instruction-level parallelism

                Vector128<uint> a0 = Vector128.LoadUnsafe(ref localField, 0 * width);
                Vector128<uint> a2 = Vector128.LoadUnsafe(ref localField, 2 * width);
                Vector128<uint> a4 = Vector128.LoadUnsafe(ref localField, 4 * width);
                Vector128<uint> a6 = Vector128.LoadUnsafe(ref localField, 6 * width);

                // store starts and ends
                Vector128<uint> x0 = a0 & endMask;
                Vector128<uint> x2 = a2 & endMask;
                Vector128<uint> x4 = a4 & endMask;
                Vector128<uint> x6 = a6 & endMask;

                x0.AsInt32().StoreUnsafe(ref localEnd, 0 * width);
                x2.AsInt32().StoreUnsafe(ref localEnd, 2 * width);
                x4.AsInt32().StoreUnsafe(ref localEnd, 4 * width);
                x6.AsInt32().StoreUnsafe(ref localEnd, 6 * width);

                (x0 + ((a0 >> 30) & one) + one).AsInt32().StoreUnsafe(ref localStart, 0 * width);
                (x2 + ((a2 >> 30) & one) + one).AsInt32().StoreUnsafe(ref localStart, 2 * width);
                (x4 + ((a4 >> 30) & one) + one).AsInt32().StoreUnsafe(ref localStart, 4 * width);
                (x6 + ((a6 >> 30) & one) + one).AsInt32().StoreUnsafe(ref localStart, 6 * width);

                // narrow even
                Vector64<short> b0 = AdvSimd.ExtractNarrowingSaturateLower(a0.AsInt32());
                Vector64<short> b2 = AdvSimd.ExtractNarrowingSaturateLower(a2.AsInt32());
                Vector64<short> b4 = AdvSimd.ExtractNarrowingSaturateLower(a4.AsInt32());
                Vector64<short> b6 = AdvSimd.ExtractNarrowingSaturateLower(a6.AsInt32());

                Vector128<uint> a1 = Vector128.LoadUnsafe(ref localField, 1 * width);
                Vector128<uint> a3 = Vector128.LoadUnsafe(ref localField, 3 * width);
                Vector128<uint> a5 = Vector128.LoadUnsafe(ref localField, 5 * width);
                Vector128<uint> a7 = Vector128.LoadUnsafe(ref localField, 7 * width);

                Vector128<uint> x1 = a1 & endMask;
                Vector128<uint> x3 = a3 & endMask;
                Vector128<uint> x5 = a5 & endMask;
                Vector128<uint> x7 = a7 & endMask;

                x1.AsInt32().StoreUnsafe(ref localEnd, 1 * width);
                x3.AsInt32().StoreUnsafe(ref localEnd, 3 * width);
                x5.AsInt32().StoreUnsafe(ref localEnd, 5 * width);
                x7.AsInt32().StoreUnsafe(ref localEnd, 7 * width);

                Vector128<short> c0 = AdvSimd.ExtractNarrowingSaturateUpper(b0, a1.AsInt32());
                Vector128<short> c2 = AdvSimd.ExtractNarrowingSaturateUpper(b4, a5.AsInt32());
                Vector128<short> c1 = AdvSimd.ExtractNarrowingSaturateUpper(b2, a3.AsInt32());
                Vector128<short> c3 = AdvSimd.ExtractNarrowingSaturateUpper(b6, a7.AsInt32());

                (x1 + ((a1 >> 30) & one) + one).AsInt32().StoreUnsafe(ref localStart, 1 * width);
                (x3 + ((a3 >> 30) & one) + one).AsInt32().StoreUnsafe(ref localStart, 3 * width);
                (x5 + ((a5 >> 30) & one) + one).AsInt32().StoreUnsafe(ref localStart, 5 * width);
                (x7 + ((a7 >> 30) & one) + one).AsInt32().StoreUnsafe(ref localStart, 7 * width);

                // compute movemask

                // narrow even
                Vector64<sbyte> d0 = AdvSimd.ExtractNarrowingSaturateLower(c0);
                Vector64<sbyte> d1 = AdvSimd.ExtractNarrowingSaturateLower(c2);

                // narrow odd
                Vector128<sbyte> e0 = AdvSimd.ExtractNarrowingSaturateUpper(d0, c1);
                Vector128<sbyte> e1 = AdvSimd.ExtractNarrowingSaturateUpper(d1, c3);

                // convert to 0xFF or 0x00 (required by movemask emulation)
                Vector128<byte> r0 = AdvSimd.ShiftRightArithmetic(e0, 7).AsByte();
                Vector128<byte> r1 = AdvSimd.ShiftRightArithmetic(e1, 7).AsByte();

                uint mask = Vector256.Create(r0, r1).MoveMask();

                while (mask != 0)
                {
                    int bit = BitOperations.TrailingZeroCount(mask);
                    mask = Bithacks.ResetLowestSetBit(mask);
                    Unsafe.Add(ref eol, idx++) = (ushort)(pos + bit + 1);
                }

                pos += Vector256<byte>.Count;
            }
        }
        // x86 and wasm (guard vector length against possible SVE in the future)
        else if (Vector.IsHardwareAccelerated && Vector<byte>.Count is (16 or 32 or 64))
        {
            nint unrolledEnd = end - (2 * Vector<int>.Count);

            Vector<uint> vecEndMask = new(EndMask);

            Vector<uint> vector = Vector.LoadUnsafe(ref field, (nuint)pos);

            while (pos <= unrolledEnd)
            {
                Vector<uint> endVec = vector & vecEndMask;
                Vector<uint> startVec = ((vector >> 30) & Vector<uint>.One) + Vector<uint>.One + endVec;
                endVec.As<uint, int>().StoreUnsafe(ref endRef, (nuint)pos);
                startVec.As<uint, int>().StoreUnsafe(ref startRef, (nuint)pos);

                // eol is stored in the MSB so we only need to load and extract
                nuint positions = vector.MoveMask();

                vector = Vector.LoadUnsafe(ref field, (nuint)pos + (nuint)Vector<uint>.Count);

                while (positions != 0)
                {
                    int bit = BitOperations.TrailingZeroCount(positions);
                    positions = Bithacks.ResetLowestSetBit(positions);
                    Unsafe.Add(ref eol, idx++) = (ushort)(pos + bit + 1);
                }

                pos += Vector<uint>.Count;
            }
        }

        while (pos < end)
        {
            uint current = Unsafe.Add(ref field, pos);

            Unsafe.Add(ref startRef, pos) = NextStart(current);
            Unsafe.Add(ref endRef, pos) = End(current);

            pos++;

            // if msb is set, int32 is negative
            if (unchecked((int)current) < 0)
            {
                Unsafe.Add(ref eol, idx++) = (ushort)pos;
            }
        }

        _eolCount += (int)idx;
        return (int)idx;
    }

    /// <summary>
    /// This should be called to ensure massive records without a newline can fit in the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool EnsureCapacity()
    {
        ObjectDisposedException.ThrowIf(_fields.Length == 0, this);

        Debug.Assert(_fields.Length == _quotes.Length);
        Debug.Assert(_fields.Length == _eols.Length);

        if (_fieldCount >= (_fields.Length * 15 / 16))
        {
            if (_fieldCount >= (ushort.MaxValue * 15 / 16))
            {
                throw new NotSupportedException(
                    $"The record has too many fields ({_fieldCount}), only up to {ushort.MaxValue} are supported."
                );
            }

            ArrayPool<uint>.Shared.Resize(ref _fields, _fields.Length * 2);
            ArrayPool<byte>.Shared.Resize(ref _quotes, _quotes.Length * 2);
            ArrayPool<int>.Shared.Resize(ref _starts, _starts.Length * 2);
            ArrayPool<int>.Shared.Resize(ref _ends, _ends.Length * 2);
            ArrayPool<ushort>.Shared.Resize(ref _eols, _eols.Length * 2);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets the buffer, returning the number of characters consumed since the last reset.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Reset()
    {
        // nothing yet
        if (_fieldIndex == 0 || _fieldCount == 0)
        {
            return 0;
        }

        return ResetCore();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ResetCore()
    {
        uint lastRead = _fields[_fieldIndex];
        int offset = NextStart(lastRead);

        if (_fieldCount > _fieldIndex)
        {
            int length = _fieldCount - _fieldIndex;
            int start = _fieldIndex + 1;

            for (int i = 0; i < length; i++)
            {
                int pos = start + i;
                _fields[pos] -= (uint)offset;
                _starts[pos] -= offset;
                _ends[pos] -= offset;
            }

            _fields.AsSpan(start, length).CopyTo(_fields.AsSpan(1));
            _starts.AsSpan(start, length).CopyTo(_starts.AsSpan(1));
            _ends.AsSpan(start, length).CopyTo(_ends.AsSpan(1));
            _quotes.AsSpan(start, length).CopyTo(_quotes.AsSpan(1));

            _fields[0] = 0; // reset start of data
            _starts[0] = 0;
            _ends[0] = 0;
            _quotes.AsSpan(1 + length, _fieldCount - length).Clear(); // Clear stale quote data beyond the copied fields
#if DEBUG
            if (_quotes.AsSpan(1 + length).IndexOfAnyExcept<byte>(0) is int idx && idx >= 0)
            {
                throw new InvalidOperationException("Quote buffer was not properly cleared.");
            }
#endif
        }
        else
        {
            // TODO: delete this branch and make a test for it!

            // no unread fields
            _fields[0] = 0;
            _starts[0] = 0;
            _ends[0] = 0;
            _quotes.AsSpan(1, _fieldCount).Clear();
        }

        _fieldCount -= _fieldIndex;
        _fieldIndex = 0;
        _eolCount = 0;
        _eolIndex = 0;

        Debug.Assert(_fields[0] == 0);
        Debug.Assert((lastRead & IsEOL) != 0, "Last read record must have EOL flag set");
        Debug.Assert(_fieldCount >= 0, $"Count should be >= 0, was {_fieldCount}");

        return offset;
    }

    /// <summary>
    /// Attempts to load the next record from the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out RecordView record)
    {
        ref ushort previous = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_eols), (uint)_eolIndex);
        int flag = _eolIndex == 0 ? int.MinValue : 0;

        if (_eolIndex < _eolCount)
        {
            ushort eol = Unsafe.Add(ref previous, 1);
            int count = eol - previous + 1;

            record = new RecordView((uint)(previous | flag), count);
            _fieldIndex = eol;
            _eolIndex++;
            return true;
        }
        else
        {
            Unsafe.SkipInit(out record);
        }

        return false;
    }

    [MemberNotNull(nameof(_fields))]
    [MemberNotNull(nameof(_quotes))]
    [MemberNotNull(nameof(_eols))]
    [MemberNotNull(nameof(_starts))]
    [MemberNotNull(nameof(_ends))]
    public void Initialize(int bufferSize = DefaultFieldBufferSize)
    {
        ArrayPool<uint>.Shared.EnsureCapacity(ref _fields, bufferSize);
        ArrayPool<byte>.Shared.EnsureCapacity(ref _quotes, bufferSize);
        ArrayPool<ushort>.Shared.EnsureCapacity(ref _eols, bufferSize);
        ArrayPool<int>.Shared.EnsureCapacity(ref _starts, bufferSize);
        ArrayPool<int>.Shared.EnsureCapacity(ref _ends, bufferSize);

        _fields[0] = 0;
        _starts[0] = 0;
        _eols[0] = 0;

        _quotes.AsSpan().Clear();
        _eols.AsSpan().Clear();
        _fieldIndex = 0;
        _fieldCount = 0;

        _eolIndex = 0;
        _eolCount = 0;
    }

    public void Dispose()
    {
        _fieldIndex = 0;
        _fieldCount = 0;
        _eolIndex = 0;
        _eolCount = 0;

        ArrayPool<uint>.Shared.EnsureReturned(ref _fields);
        ArrayPool<byte>.Shared.EnsureReturned(ref _quotes);
        ArrayPool<ushort>.Shared.EnsureReturned(ref _eols);
        ArrayPool<int>.Shared.EnsureReturned(ref _starts);
        ArrayPool<int>.Shared.EnsureReturned(ref _ends);
    }

    public override string ToString() =>
        _fields.Length == 0
            ? "{ Empty }"
            : $"{{ {_fieldCount} read, {_fieldCount - _fieldIndex} available, range: [{NextStart(_fields[_fieldIndex])}..{NextStart(_fields[_fieldCount])}] }}";

    private class MetaBufferDebugView
    {
        private readonly RecordBuffer _buffer;

        public MetaBufferDebugView(RecordBuffer buffer)
        {
            _buffer = buffer;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public string[] Items =>
            _buffer._fields.Length == 0
                ? []
                :
                [
                    .. _buffer
                        ._fields.Skip(Math.Max(1, _buffer._fieldIndex))
                        .Take(_buffer._fieldIndex)
                        .Select(f => $"End: {f & EndMask}, EOL: {(f & IsEOL) != 0}"),
                ];
    }

#if DEBUG
    internal IEnumerable<string> DebugFieldsAll => _fields.Select(FormatField);
    internal string[] DebugFieldsUnread =>
        DebugFieldsAll.Skip(Math.Max(1, _fieldIndex)).Take(_fieldCount - _fieldIndex).ToArray();

    private static string FormatField(uint f)
    {
        var vsb = new ValueStringBuilder(stackalloc char[32]);
        vsb.Append("End: ");
        vsb.AppendFormatted(f & EndMask);
        vsb.Append(", EOL: ");
        vsb.Append(
            (f & IsEOL) switch
            {
                IsCRLF => "CRLF",
                IsEOL => "LF",
                (IsEOL >> 1) => "Invalid",
                _ => "None",
            }
        );

        vsb.Append(" [");
        vsb.AppendFormatted(f, "X8");
        vsb.Append(']');
        return vsb.ToString();
    }
#endif
}
