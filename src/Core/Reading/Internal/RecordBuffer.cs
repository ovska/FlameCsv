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
        }

        startIndex = _fieldCount == 0 ? 0 : NextStart(_fields[_fieldCount]);
        return new()
        {
            //
            Fields = _fields.AsSpan(start),
            Quotes = _quotes.AsSpan(start),
        };
    }

    public int BufferedFields
    {
        get
        {
            ObjectDisposedException.ThrowIf(_fields.Length == 0, this);
            return _fieldCount - _fieldIndex;
        }
    }

    public int BufferedDataLength
    {
        get
        {
            ObjectDisposedException.ThrowIf(_fields.Length == 0, this);
            return NextStart(_fields[_fieldCount]);
        }
    }

    /// <summary>
    /// Marks fields as read, and returns the end position of the last field.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetFieldsRead(int count)
    {
        Debug.Assert(count >= 0);
        Debug.Assert((_fieldCount + count) < _fields.Length);

        _fieldCount += count;

        ref ushort eol = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_eols), (uint)_eolIndex + 1u);
        ref uint field = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_fields), (uint)_fieldIndex + 1u);

        nint end = _fieldCount - _fieldIndex;
        nint pos = 0;

        nuint idx = 0;

        // arm64
        if (AdvSimd.IsSupported)
        {
            nint unrolledEnd = end - Vector256<byte>.Count;

            while (pos <= unrolledEnd)
            {
                uint mask = AsciiVector
                    .LoadInt32SignsToByteMasksARM(ref Unsafe.As<uint, int>(ref field), (nuint)pos)
                    .MoveMask();

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

            Vector<uint> vector = Vector.LoadUnsafe(ref field, (nuint)pos);

            while (pos <= unrolledEnd)
            {
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
            // if msb is set, int32 is negative
            if (unchecked((int)Unsafe.Add(ref field, pos++)) < 0)
            {
                Unsafe.Add(ref eol, idx++) = (ushort)pos;
            }
        }

        _eolCount += (int)idx;
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

            Span<uint> buffer = _fields.AsSpan(start, length);

            // Preserve the EOL flags while shifting only the end position
            foreach (ref uint value in buffer)
            {
                uint flags = value & ~EndMask;
                uint shiftedEnd = (value & EndMask) - (uint)offset;
                value = shiftedEnd | flags;
            }

            buffer.CopyTo(_fields.AsSpan(1));
            _fields[0] = 0; // reset start of data
#if DEBUG
            // for debugging
            _fields.AsSpan(buffer.Length + 1).Fill(~0u);
#endif

            _quotes.AsSpan(start, length).CopyTo(_quotes.AsSpan(1));

            // the quote buffer must be cleared
            _quotes.AsSpan(1 + start + length).Clear();
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

    [MemberNotNull(nameof(_fields)), MemberNotNull(nameof(_quotes)), MemberNotNull(nameof(_eols))]
    public void Initialize(int bufferSize = DefaultFieldBufferSize)
    {
        ArrayPool<uint>.Shared.EnsureCapacity(ref _fields, bufferSize);
        ArrayPool<byte>.Shared.EnsureCapacity(ref _quotes, bufferSize);
        ArrayPool<ushort>.Shared.EnsureCapacity(ref _eols, bufferSize);

        _fields[0] = 0;
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
