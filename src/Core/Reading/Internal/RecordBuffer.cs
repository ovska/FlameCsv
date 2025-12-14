using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Intrinsics;
using FlameCsv.Utilities;
using static FlameCsv.Reading.Internal.Field;

namespace FlameCsv.Reading.Internal;

[DebuggerDisplay("{ToString(),nq}")]
[SkipLocalsInit]
internal sealed class RecordBuffer : IDisposable
{
    public const int DefaultFieldBufferSize = 4096;

    /// <summary>
    /// Storage for the raw field metadata.
    /// </summary>
    internal uint[] _fields;

    /// <summary>
    /// Storage for quote counts.
    /// </summary>
    internal byte[] _quotes;

    /// <summary>
    /// Storage for bit packed field metadata.
    /// </summary>
    internal ulong[] _bits;

    /// <summary>
    /// Storage for EOL field indices.
    /// </summary>
    internal ushort[] _eols;

    internal int _eolIndex;
    internal int _eolCount;

    /// <summary>
    /// Number of fields that have been consumed from the buffer.
    /// </summary>
    private int FieldIndex => _eols[_eolIndex];

    /// <summary>
    /// Number of fields that have been parsed to the buffer.
    /// </summary>
    internal int _fieldCount;

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
            ArrayPool<ulong>.Shared.Resize(ref _bits, newLength);
        }

        startIndex = _fieldCount == 0 ? 0 : NextStart(_fields[_fieldCount]);
        return new FieldBuffer(start, this);
    }

    /// <summary>
    /// Number of fields that have been buffered and not yet consumed.
    /// </summary>
    public int UnreadFields
    {
        get
        {
            ObjectDisposedException.ThrowIf(_fields.Length == 0, this);
            return _fieldCount - FieldIndex;
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
    public unsafe int SetFieldsRead(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        Debug.Assert(count >= 0);
        Debug.Assert((_fieldCount + count) < _fields.Length);

        _fieldCount += count;

        int fieldIndex = FieldIndex;

        ref ushort eol = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_eols), (uint)_eolIndex + 1u);

        ref uint field = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_fields), (uint)fieldIndex + 1u);
        ref byte quoteRef = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_quotes), (uint)fieldIndex + 1u);
        ref ulong bitsRef = ref MemoryMarshal.GetArrayDataReference(_bits);

        nint end = _fieldCount - fieldIndex;
        nint pos = 0;

        nuint idx = 0;

        if (Vector.IsHardwareAccelerated && Vector<byte>.Count is (16 or 32 or 64))
        {
            // unroll by 4 uint vectors for potentially more ILP, and to keep ARM64 movemask emulation efficient
            nint unrolledEnd = end - Vector<byte>.Count;
            Vector<uint> endMask = Vector.Create(EndMask);
            nuint width = (nuint)Vector<uint>.Count;

            fixed (uint* pField = &field)
            fixed (ulong* pBits = &bitsRef)
            {
                while (pos <= unrolledEnd)
                {
                    nuint mask;
                    Vector<uint> a0,
                        a1,
                        a2,
                        a3;

                    if (AdvSimd.Arm64.IsSupported)
                    {
                        (a0, a1) = Unsafe.BitCast<(Vector128<uint>, Vector128<uint>), (Vector<uint>, Vector<uint>)>(
                            AdvSimd.Arm64.LoadPairVector128(pField + pos)
                        );

                        (a2, a3) = Unsafe.BitCast<(Vector128<uint>, Vector128<uint>), (Vector<uint>, Vector<uint>)>(
                            AdvSimd.Arm64.LoadPairVector128(pField + pos + (width * 2))
                        );

                        Vector64<short> b0 = AdvSimd.ExtractNarrowingSaturateLower(a0.AsVector128().AsInt32());
                        Vector64<short> b2 = AdvSimd.ExtractNarrowingSaturateLower(a2.AsVector128().AsInt32());
                        Vector128<short> c0 = AdvSimd.ExtractNarrowingSaturateUpper(b0, a1.AsVector128().AsInt32());
                        Vector128<short> c1 = AdvSimd.ExtractNarrowingSaturateUpper(b2, a3.AsVector128().AsInt32());
                        Vector64<sbyte> d0 = AdvSimd.ExtractNarrowingSaturateLower(c0);
                        Vector128<sbyte> e0 = AdvSimd.ExtractNarrowingSaturateUpper(d0, c1);

                        // convert to 0xFF or 0x00 with a sign-extending shift (required by movemask emulation)
                        Vector128<byte> r0 = AdvSimd.ShiftRightArithmetic(e0, 7).AsByte();
                        mask = r0.MoveMask();
                    }
                    else
                    {
                        ref uint localField = ref Unsafe.Add(ref field, (uint)pos);
                        a0 = Vector.LoadUnsafe(ref localField, width * 0);
                        a1 = Vector.LoadUnsafe(ref localField, width * 1);
                        a2 = Vector.LoadUnsafe(ref localField, width * 2);
                        a3 = Vector.LoadUnsafe(ref localField, width * 3);

                        // shift MSB's to byte msb position
                        Vector<ushort> b0 = Vector.Narrow(a0 >> 24, a1 >> 24);
                        Vector<ushort> b1 = Vector.Narrow(a2 >> 24, a3 >> 24);
                        Vector<byte> r0 = Vector.Narrow(b0, b1);
                        mask = r0.MoveMask();
                    }

                    Vector<uint> end0 = a0 & endMask;
                    Vector<uint> end1 = a1 & endMask;
                    Vector<uint> end2 = a2 & endMask;
                    Vector<uint> end3 = a3 & endMask;

                    Vector<uint> start0 = end0 + Vector<uint>.One;
                    Vector<uint> start1 = end1 + Vector<uint>.One;
                    Vector<uint> start2 = end2 + Vector<uint>.One;
                    Vector<uint> start3 = end3 + Vector<uint>.One;

                    Vector<byte> quotVec = Vector.LoadUnsafe(ref quoteRef, (nuint)pos);

                    // add 1 for CRLF fields
                    start0 += ((a0 >> 30) & Vector<uint>.One);
                    start1 += ((a1 >> 30) & Vector<uint>.One);
                    start2 += ((a2 >> 30) & Vector<uint>.One);
                    start3 += ((a3 >> 30) & Vector<uint>.One);

                    if (quotVec != Vector<byte>.Zero)
                    {
                        // load quotes. store "any quotes" to MSB, "needs unescaping" to 2nd MSB
                        Vector<byte> nonzero = Vector.GreaterThan(quotVec, Vector<byte>.Zero);
                        Vector<byte> over2 = Vector.GreaterThan(quotVec, Vector.Create((byte)2));

                        // create the quote mask; needsUnescaping can overlap as it implies hasQuotes
                        Vector<byte> hasQ = nonzero << 7;
                        Vector<byte> needsUnescaping = over2 << 6;
                        Vector<byte> quoteMask = hasQ | needsUnescaping;

                        // do two shift widens to keep the flags at the most significant bits
                        // TODO: NEON shift+widen op when imm8 fixed
                        Vector<ushort> qLo = Vector.WidenLower(quoteMask);
                        Vector<ushort> qHi = Vector.WidenUpper(quoteMask);
                        Vector<uint> qu0 = Vector.WidenLower(qLo);
                        Vector<uint> qu1 = Vector.WidenUpper(qLo);
                        Vector<uint> qu2 = Vector.WidenLower(qHi);
                        Vector<uint> qu3 = Vector.WidenUpper(qHi);

                        // shift bits to the correct positions
                        qu0 <<= 24;
                        qu1 <<= 24;
                        qu2 <<= 24;
                        qu3 <<= 24;

                        // flag the ends with quotes info
                        end0 |= qu0;
                        end1 |= qu1;
                        end2 |= qu2;
                        end3 |= qu3;
                    }

                    // store the starts and ends zipped so they can be read as a single ulong later
                    // offset by 1 so first start is 0
                    uint* dst = (uint*)(pBits + pos) + 1;

                    if (AdvSimd.Arm64.IsSupported)
                    {
                        AdvSimd.Arm64.StoreVectorAndZip(dst + (width * 0), (end0.AsVector128(), start0.AsVector128()));
                        AdvSimd.Arm64.StoreVectorAndZip(dst + (width * 2), (end1.AsVector128(), start1.AsVector128()));
                        AdvSimd.Arm64.StoreVectorAndZip(dst + (width * 4), (end2.AsVector128(), start2.AsVector128()));
                        AdvSimd.Arm64.StoreVectorAndZip(dst + (width * 6), (end3.AsVector128(), start3.AsVector128()));
                    }
                    else if (Vector<byte>.Count is 32 && Avx2.IsSupported)
                    {
                        Vector256<uint> lo0 = Avx2.UnpackLow(end0.AsVector256(), start0.AsVector256());
                        Vector256<uint> hi0 = Avx2.UnpackHigh(end0.AsVector256(), start0.AsVector256());
                        Vector256<uint> lo1 = Avx2.UnpackLow(end1.AsVector256(), start1.AsVector256());
                        Vector256<uint> hi1 = Avx2.UnpackHigh(end1.AsVector256(), start1.AsVector256());
                        Vector256<uint> lo2 = Avx2.UnpackLow(end2.AsVector256(), start2.AsVector256());
                        Vector256<uint> hi2 = Avx2.UnpackHigh(end2.AsVector256(), start2.AsVector256());
                        Vector256<uint> lo3 = Avx2.UnpackLow(end3.AsVector256(), start3.AsVector256());
                        Vector256<uint> hi3 = Avx2.UnpackHigh(end3.AsVector256(), start3.AsVector256());

                        const byte ctrl0 = 0b100000; // 00 10 00 00
                        const byte ctrl1 = 0b110001; // 00 11 00 01
                        Avx2.Permute2x128(lo0, hi0, ctrl0).Store(dst + (width * 0));
                        Avx2.Permute2x128(lo0, hi0, ctrl1).Store(dst + (width * 1));
                        Avx2.Permute2x128(lo1, hi1, ctrl0).Store(dst + (width * 2));
                        Avx2.Permute2x128(lo1, hi1, ctrl1).Store(dst + (width * 3));
                        Avx2.Permute2x128(lo2, hi2, ctrl0).Store(dst + (width * 4));
                        Avx2.Permute2x128(lo2, hi2, ctrl1).Store(dst + (width * 5));
                        Avx2.Permute2x128(lo3, hi3, ctrl0).Store(dst + (width * 6));
                        Avx2.Permute2x128(lo3, hi3, ctrl1).Store(dst + (width * 7));
                    }
                    else if (Vector<byte>.Count is 16 && Sse2.IsSupported)
                    {
                        Sse2.UnpackLow(end0.AsVector128(), start0.AsVector128()).Store(dst + (width * 0));
                        Sse2.UnpackHigh(end0.AsVector128(), start0.AsVector128()).Store(dst + (width * 1));
                        Sse2.UnpackLow(end1.AsVector128(), start1.AsVector128()).Store(dst + (width * 2));
                        Sse2.UnpackHigh(end1.AsVector128(), start1.AsVector128()).Store(dst + (width * 3));
                        Sse2.UnpackLow(end2.AsVector128(), start2.AsVector128()).Store(dst + (width * 4));
                        Sse2.UnpackHigh(end2.AsVector128(), start2.AsVector128()).Store(dst + (width * 5));
                        Sse2.UnpackLow(end3.AsVector128(), start3.AsVector128()).Store(dst + (width * 6));
                        Sse2.UnpackHigh(end3.AsVector128(), start3.AsVector128()).Store(dst + (width * 7));
                    }
                    else
                    {
                        // csharpier-ignore
                        {
                            (Vector.WidenLower(end0) | (Vector.WidenLower(start0) << 32)).As<ulong, uint>().Store(dst + (width * 0));
                            (Vector.WidenUpper(end0) | (Vector.WidenUpper(start0) << 32)).As<ulong, uint>().Store(dst + (width * 1));
                            (Vector.WidenLower(end1) | (Vector.WidenLower(start1) << 32)).As<ulong, uint>().Store(dst + (width * 2));
                            (Vector.WidenUpper(end1) | (Vector.WidenUpper(start1) << 32)).As<ulong, uint>().Store(dst + (width * 3));
                            (Vector.WidenLower(end2) | (Vector.WidenLower(start2) << 32)).As<ulong, uint>().Store(dst + (width * 4));
                            (Vector.WidenUpper(end2) | (Vector.WidenUpper(start2) << 32)).As<ulong, uint>().Store(dst + (width * 5));
                            (Vector.WidenLower(end3) | (Vector.WidenLower(start3) << 32)).As<ulong, uint>().Store(dst + (width * 6));
                            (Vector.WidenUpper(end3) | (Vector.WidenUpper(start3) << 32)).As<ulong, uint>().Store(dst + (width * 7));
                        }
                    }

                    while (mask != 0)
                    {
                        int tz = BitOperations.TrailingZeroCount(mask);
                        mask = Bithacks.ResetLowestSetBit(mask);
                        Unsafe.Add(ref eol, idx++) = (ushort)(pos + tz + 1);
                    }

                    pos += (nint)(width * 4);
                }
            }
        }

        while (pos < end)
        {
            uint current = Unsafe.Add(ref field, pos);
            byte quote = Unsafe.Add(ref quoteRef, pos);

            int curEnd = End(current);
            int nextStart = NextStart(current);
            int hasQuotes = (quote != 0).ToByte();
            int needsUnescaping = (quote > 2).ToByte();

            Debug.Assert(
                nextStart > curEnd,
                $"Start position should not be greater than end position: {nextStart}..{curEnd}"
            );
            Debug.Assert(
                nextStart <= MaxFieldEnd,
                $"Positions should not exceed maximum field: {nextStart} > {MaxFieldEnd}"
            );

            ref uint dst = ref Unsafe.As<ulong, uint>(ref Unsafe.Add(ref bitsRef, pos));
            Unsafe.Add(ref dst, 1) = (uint)(curEnd | (hasQuotes << 31) | (needsUnescaping << 30));
            Unsafe.Add(ref dst, 2) = (uint)nextStart;

            pos++;

            // if msb is set, int32 is negative
            if ((int)current < 0)
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
            ArrayPool<ulong>.Shared.Resize(ref _bits, _bits.Length * 2);
            ArrayPool<ushort>.Shared.Resize(ref _eols, _eols.Length * 2);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets the buffer, returning the number of characters consumed since the last reset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Reset()
    {
        // either we haven't read yet, or the buffered records haven't been consumed at all
        if (_eolIndex == 0)
        {
            return 0;
        }

        return ResetCore();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ResetCore()
    {
        ObjectDisposedException.ThrowIf(_fields.Length == 0, this);

        Debug.Assert(FieldIndex > 0);

        int fieldIndex = FieldIndex;

        // no unread fields
        _fields[0] = 0;
        _bits[0] = 0;
        _quotes.AsSpan(1, _fieldCount).Clear();
        _fieldCount = 0;
        _eolCount = 0;
        _eolIndex = 0;

#if DEBUG
        if (_quotes.ContainsAnyExcept((byte)0))
        {
            throw new InvalidOperationException("Quote buffer was not properly cleared.");
        }
#endif

        return (int)_bits[fieldIndex];
    }

    /// <summary>
    /// Attempts to load the next record from the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out RecordView view)
    {
        ref ushort previous = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_eols), (uint)_eolIndex);

        if (_eolIndex < _eolCount)
        {
            ushort eol = Unsafe.Add(ref previous, 1);
            int count = eol - previous;
            view = new(previous, count);
            _eolIndex++;
            return true;
        }
        else
        {
            Unsafe.SkipInit(out view);
        }

        return false;
    }

    [MemberNotNull(nameof(_fields))]
    [MemberNotNull(nameof(_quotes))]
    [MemberNotNull(nameof(_eols))]
    [MemberNotNull(nameof(_bits))]
    public void Initialize(int bufferSize = DefaultFieldBufferSize)
    {
        ArrayPool<uint>.Shared.EnsureCapacity(ref _fields, bufferSize);
        ArrayPool<byte>.Shared.EnsureCapacity(ref _quotes, bufferSize);
        ArrayPool<ushort>.Shared.EnsureCapacity(ref _eols, bufferSize);
        ArrayPool<ulong>.Shared.EnsureCapacity(ref _bits, bufferSize);

        _fields[0] = 0;
        _bits[0] = 0;
        _eols[0] = 0;

        _quotes.AsSpan().Clear();
        _eols.AsSpan().Clear();
        _fieldCount = 0;

        _eolIndex = 0;
        _eolCount = 0;
    }

    public void Dispose()
    {
        _fieldCount = 0;
        _eolIndex = 0;
        _eolCount = 0;

        ArrayPool<uint>.Shared.EnsureReturned(ref _fields);
        ArrayPool<byte>.Shared.EnsureReturned(ref _quotes);
        ArrayPool<ushort>.Shared.EnsureReturned(ref _eols);
        ArrayPool<ulong>.Shared.EnsureReturned(ref _bits);
    }

#if DEBUG
    internal IEnumerable<string> DebugFieldsUnread =>
        DebugFields.Skip(Math.Max(1, FieldIndex)).Take(_fieldCount - FieldIndex);

    internal IEnumerable<string> DebugFields
    {
        get
        {
            EnumeratorStack es = default;

            for (int i = 1; i < _fields.Length; i++)
            {
                var vsb = new ValueStringBuilder(MemoryMarshal.Cast<byte, char>((Span<byte>)es));
                ulong bits = _bits[i - 1];
                int start = (int)bits;
                int end = (int)(bits >> 32) & (int)EndMask;
                byte quotes = _quotes[i];

                vsb.AppendFormatted(start, "D6");
                vsb.Append("..");
                vsb.AppendFormatted(end, "D6");

                if ((int)_fields[i] < 0)
                {
                    if ((_fields[i] & IsCRLF) != 0)
                    {
                        vsb.Append(" CRLF");
                    }
                    else
                    {
                        vsb.Append(" LF");
                    }
                }

                if (quotes != 0)
                {
                    vsb.Append(" Q:");
                    vsb.AppendFormatted(quotes);
                }

                yield return vsb.ToString();
            }
        }
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetLengthWithNewline(RecordView view)
    {
        ref ulong first = ref _bits[0];
        int recordStart = (int)Unsafe.Add(ref first, (uint)view.Start);
        int nextRecordStart = (int)Unsafe.Add(ref first, (uint)view.Start + (uint)view.Length);
        return nextRecordStart - recordStart;
    }
}
