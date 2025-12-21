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

[DebuggerDisplay("{ToString(),nq}")]
[SkipLocalsInit]
internal sealed class RecordBuffer : IDisposable
{
    public const int DefaultFieldBufferSize = 4096;

    /// <summary>
    /// Sentinel data for the first field. This ensures that adding 1 will roll over to 0,
    /// and it does not have CRLF bit set.
    /// </summary>
    internal const uint FirstSentinel = EndMask;

    /// <summary>
    /// Storage for the raw field metadata.
    /// </summary>
    internal uint[] _fields;

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
    public Span<uint> GetUnreadBuffer(int minimumLength, out int startIndex)
    {
        int start = _fieldCount + 1;

        // TODO: this might be a dead path?
        if ((_fields.Length - start) < minimumLength)
        {
            int newLength = Math.Max(_fields.Length * 2, minimumLength + start);
            ArrayPool<uint>.Shared.Resize(ref _fields, newLength);
            ArrayPool<ushort>.Shared.Resize(ref _eols, newLength);
        }

        startIndex = _fieldCount == 0 ? 0 : NextStart(_fields[_fieldCount]);
        return _fields.AsSpan(start);
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

            Debug.Assert(_fields[0] is FirstSentinel, "First field should indicate start of data");

            if (_eolCount == 0)
            {
                return 0;
            }

            uint lastField = _fields[_eols[_eolCount]];
            return NextStartCRLFAware(lastField);
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

        nint end = _fieldCount - fieldIndex;
        nint pos = 0;

        nuint idx = 0;

        if (Vector.IsHardwareAccelerated && Vector<byte>.Count is (16 or 32 or 64))
        {
            // unroll by 4 uint vectors for potentially more ILP, and to keep ARM64 movemask emulation efficient
            nint unrolledEnd = end - Vector<byte>.Count;
            Vector<uint> endMask = Vector.Create(EndMask);
            int width = Vector<uint>.Count;

            fixed (uint* pField = &field)
            {
                while (pos <= unrolledEnd)
                {
                    nuint mask;
                    uint* pFieldCurrent = pField + pos;

                    if (AdvSimd.Arm64.IsSupported)
                    {
                        var (a0, a1) = AdvSimd.Arm64.LoadPairVector128(pFieldCurrent);
                        var (a2, a3) = AdvSimd.Arm64.LoadPairVector128(pFieldCurrent + (width * 2));

                        Vector64<short> b0 = AdvSimd.ExtractNarrowingSaturateLower(a0.AsInt32());
                        Vector64<short> b2 = AdvSimd.ExtractNarrowingSaturateLower(a2.AsInt32());
                        Vector128<short> c0 = AdvSimd.ExtractNarrowingSaturateUpper(b0, a1.AsInt32());
                        Vector128<short> c1 = AdvSimd.ExtractNarrowingSaturateUpper(b2, a3.AsInt32());
                        Vector64<sbyte> d0 = AdvSimd.ExtractNarrowingSaturateLower(c0);
                        Vector128<sbyte> e0 = AdvSimd.ExtractNarrowingSaturateUpper(d0, c1);

                        // convert to 0xFF or 0x00 with a sign-extending shift (required by movemask emulation)
                        Vector128<byte> r0 = AdvSimd.ShiftRightArithmetic(e0, 7).AsByte();
                        mask = r0.MoveMask();
                    }
                    else
                    {
                        Vector<uint> a0 = Vector.Load(pFieldCurrent + ((nuint)width * 0));
                        Vector<uint> a1 = Vector.Load(pFieldCurrent + ((nuint)width * 1));
                        Vector<uint> a2 = Vector.Load(pFieldCurrent + ((nuint)width * 2));
                        Vector<uint> a3 = Vector.Load(pFieldCurrent + ((nuint)width * 3));
                        nuint m0 = (uint)a0.MoveMask();
                        nuint m1 = (uint)a1.MoveMask();
                        nuint m2 = (uint)a2.MoveMask();
                        nuint m3 = (uint)a3.MoveMask();
                        mask = (m0 << (width * 0)) | (m1 << (width * 1)) | (m2 << (width * 2)) | (m3 << (width * 3));
                    }

                    while (mask != 0)
                    {
                        int tz = BitOperations.TrailingZeroCount(mask);
                        mask = Bithacks.ResetLowestSetBit(mask);
                        Unsafe.Add(ref eol, idx++) = (ushort)(pos + tz + 1);
                    }

                    pos += (width * 4);
                }
            }
        }

        while (pos < end)
        {
            if ((int)Unsafe.Add(ref field, pos) < 0)
            {
                Unsafe.Add(ref eol, idx++) = (ushort)(pos + 1);
            }

            pos++;
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

        Debug.Assert(_fields.Length == _eols.Length);

        const int hardLimit = ushort.MaxValue - 256;

        if (_fieldCount >= (_fields.Length * 15 / 16))
        {
            if (_fieldCount >= hardLimit)
            {
                throw new InvalidDataException(
                    $"The record has too many fields ({_fieldCount}), only up to {hardLimit} are supported."
                );
            }

            ArrayPool<uint>.Shared.Resize(ref _fields, _fields.Length * 2);
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
        _fields[0] = FirstSentinel;
        _fieldCount = 0;
        _eolCount = 0;
        _eolIndex = 0;

        return NextStartCRLFAware(_fields[fieldIndex]);
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
            ref uint prevField = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_fields), (uint)previous);

            int count = eol - previous;
            view = new(previous, count);
            _eolIndex++;
            prevField += (prevField >> 30) & 1; // account for CRLF
            return true;
        }
        else
        {
            Unsafe.SkipInit(out view);
        }

        return false;
    }

    [MemberNotNull(nameof(_fields))]
    [MemberNotNull(nameof(_eols))]
    public void Initialize(int bufferSize = DefaultFieldBufferSize)
    {
        ArrayPool<uint>.Shared.EnsureCapacity(ref _fields, bufferSize);
        ArrayPool<ushort>.Shared.EnsureCapacity(ref _eols, bufferSize);

        _fields[0] = FirstSentinel;
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
        ArrayPool<ushort>.Shared.EnsureReturned(ref _eols);
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
                int start = NextStart(_fields[i - 1]);
                int end = End(_fields[i]);

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

                yield return vsb.ToString();
            }
        }
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetLengthWithNewline(RecordView view)
    {
        ref uint previous = ref Unsafe.Add(ref _fields[0], (uint)view.Start);
        int recordStart = NextStart(previous);
        int nextRecordStart = NextStartCRLFAware(Unsafe.Add(ref previous, (uint)view.Length));
        Debug.Assert(nextRecordStart > recordStart);
        return nextRecordStart - recordStart;
    }
}
