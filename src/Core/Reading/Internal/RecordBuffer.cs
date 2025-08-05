using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Utilities;
using JetBrains.Annotations;
using static FlameCsv.Reading.Internal.Field;

namespace FlameCsv.Reading.Internal;

internal readonly ref struct FieldBuffer
{
    public required Span<uint> Fields { get; init; }
    public required Span<byte> Quotes { get; init; }
}

[DebuggerTypeProxy(typeof(MetaBufferDebugView))]
[DebuggerDisplay("{ToString(),nq}")]
[SkipLocalsInit]
internal sealed class RecordBuffer : IDisposable
{
    /// <summary>
    /// Storage for the field metadata.
    /// </summary>
    private uint[] _fields;

    /// <summary>
    /// Storage for quote counts.
    /// </summary>
    private byte[] _quotes;

    /// <summary>
    /// Number of fields that have been consumed from the buffer.
    /// </summary>
    private int _index;

    /// <summary>
    /// Number of fields that have been parsed to the buffer.
    /// </summary>
    private int _count;

    public RecordBuffer()
    {
        Initialize();
    }

    /// <summary>
    /// Returns unconsumed meta buffer. If 3 fields were left in the buffer on last reset,
    /// returns array length - 3.
    /// </summary>
    public FieldBuffer GetUnreadBuffer(int minimumLength, out int startIndex)
    {
        ObjectDisposedException.ThrowIf(_fields.Length == 0, this);

        int start = _count + 1;

        if ((_fields.Length - start) < minimumLength)
        {
            int newLength = Math.Max(_fields.Length * 2, minimumLength + start);
            ArrayPool<uint>.Shared.Resize(ref _fields, newLength);
            ArrayPool<byte>.Shared.Resize(ref _quotes, newLength);
        }

        startIndex = NextStart(_fields[_count]);
        return new()
        {
            //
            Fields = _fields.AsSpan(start),
            Quotes = _quotes.AsSpan(start),
        };
    }

    public int BufferedDataLength
    {
        get
        {
            ObjectDisposedException.ThrowIf(_fields.Length == 0, this);
            return NextStart(_fields[_count]);
        }
    }

    /// <summary>
    /// Marks fields as read, and returns the end position of the last field.
    /// </summary>
    /// <param name="count"></param>
    public int SetFieldsRead(int count)
    {
        Debug.Assert(count >= 0);
        Debug.Assert((_count + count) < _fields.Length);
        _count += count;
        return NextStart(_fields[_count]);
    }

    /// <summary>
    /// This should be called to ensure massive records without a newline can fit in the buffer.
    /// </summary>
    /// <returns></returns>
    public void EnsureCapacity()
    {
        if (_count >= (_fields.Length * 15 / 16))
        {
            ArrayPool<uint>.Shared.Resize(ref _fields, _fields.Length * 2);
            ArrayPool<byte>.Shared.Resize(ref _quotes, _quotes.Length * 2);
        }
    }

    /// <summary>
    /// Resets the buffer, returning the number of characters consumed since the last reset.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Reset()
    {
        // nothing yet
        if (_index == 0 || _count == 0)
        {
            return 0;
        }

        return ResetCore();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ResetCore()
    {
        uint lastRead = _fields[_index];
        int offset = NextStart(lastRead);

        if (_count > _index)
        {
            int length = _count - _index;
            int start = _index + 1;

            Span<uint> buffer = _fields.AsSpan(start, length);

            // Preserve the EOL flags while shifting only the end position
            // TODO: simd?
            foreach (ref uint value in buffer)
            {
                uint flags = value & FlagMask;
                uint shiftedEnd = (value & EndMask) - (uint)offset;
                value = shiftedEnd | flags;
            }

            buffer.CopyTo(_fields.AsSpan(1));
            _fields[0] = StartOrEnd; // reset start of data
#if DEBUG
            // for debugging
            _fields.AsSpan(buffer.Length + 1).Fill(~0u);
#endif

            // clear quotes
            _quotes.AsSpan(start, length).CopyTo(_quotes.AsSpan(1));
            _quotes.AsSpan(1 + start + length).Clear();
            // the quote buffer must be clean
        }

        _count -= _index;
        _index = 0;

        Debug.Assert(_fields[0] == StartOrEnd);
        Debug.Assert((lastRead & FlagMask) != 0, "Last read record must have EOL flag set");
        Debug.Assert(_count >= 0);

        return offset;
    }

    /// <summary>
    /// Attempts to load the next record from the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out RecordView record)
    {
        Unsafe.SkipInit(out record);

        ref uint first = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_fields), (uint)_index + 1u);

        nint end = _count - _index;
        nint unrolledEnd = end - 4;
        nint pos = 0;

        while (pos < unrolledEnd)
        {
            ref uint b0 = ref Unsafe.Add(ref first, pos);

            if ((b0 & FlagMask) != 0)
            {
                goto Found1;
            }
            if ((Unsafe.Add(ref b0, 1u) & FlagMask) != 0)
            {
                goto Found2;
            }
            if ((Unsafe.Add(ref b0, 2u) & FlagMask) != 0)
            {
                goto Found3;
            }
            if ((Unsafe.Add(ref b0, 3u) & FlagMask) != 0)
            {
                goto Found4;
            }

            pos += 4;
        }

        while (pos < end)
        {
            if ((Unsafe.Add(ref first, pos++) & FlagMask) != 0)
            {
                goto Found;
            }
        }
        return false;

        Found4:
        pos += 4;
        goto Found;

        Found3:
        pos += 3;
        goto Found;

        Found2:
        pos += 2;
        goto Found;

        Found1:
        pos += 1;

        Found:
        record = new RecordView(_fields, _quotes, _index, (int)(pos + 1));
        _index += (int)pos;
        return true;
    }

    [MemberNotNull(nameof(_fields)), MemberNotNull(nameof(_quotes))]
    public void Initialize()
    {
        ArrayPool<uint>.Shared.EnsureCapacity(ref _fields, 4096);
        ArrayPool<byte>.Shared.EnsureCapacity(ref _quotes, 4096);
        _fields[0] = StartOrEnd;
        _quotes.AsSpan().Clear();
        _index = 0;
        _count = 0;
    }

    public void Dispose()
    {
        _index = 0;
        _count = 0;

        uint[] fields = _fields;
        byte[] quotes = _quotes;

        _fields = [];
        _quotes = [];

        if (fields.Length > 0)
        {
            ArrayPool<uint>.Shared.Return(fields);
        }

        if (quotes.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(quotes);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref uint[] GetFieldArrayRef() => ref _fields;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref byte[] GetQuoteArrayRef() => ref _quotes;

    public override string ToString() =>
        _fields.Length == 0
            ? "{ Empty }"
            : $"{{ {_count} read, {_count - _index} available, range: [{NextStart(_fields[_index])}..{NextStart(_fields[_count])}] }}";

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
                        ._fields.Skip(Math.Max(1, _buffer._index))
                        .Take(_buffer._index)
                        .Select(f => $"End: {f & EndMask}, EOL: {(f & StartOrEnd) != 0}"),
                ];
    }

#if DEBUG
    internal IEnumerable<string> DebugFieldsAll => _fields.Select(FormatField);
    internal string[] DebugFieldsUnread => DebugFieldsAll.Skip(Math.Max(1, _index)).Take(_count - _index).ToArray();

    private static string FormatField(uint f)
    {
        var vsb = new ValueStringBuilder(stackalloc char[32]);
        vsb.Append("End: ");
        vsb.AppendFormatted(f & EndMask);
        vsb.Append(", EOL: ");
        vsb.Append(
            (f & FlagMask) switch
            {
                StartOrEnd => "Start",
                IsEOL => "LF",
                IsCRLF => "CRLF",
                _ => "None",
            }
        );

        vsb.Append(" [");
        vsb.AppendFormatted(f, "X8");
        vsb.Append(']');
        return vsb.ToString();
    }
#endif

    internal struct UnsafeSegment<T>
        where T : unmanaged
    {
        [UsedImplicitly]
        public T[]? array;

        [UsedImplicitly]
        public int offset;

        [UsedImplicitly]
        public int count;

#if DEBUG
        static UnsafeSegment()
        {
            if (Unsafe.SizeOf<UnsafeSegment<T>>() != Unsafe.SizeOf<ArraySegment<T>>())
            {
                throw new InvalidOperationException("MetaSegment has unexpected size");
            }

            var array = new T[4];
            var segment = new UnsafeSegment<T>
            {
                array = array,
                offset = 1,
                count = 2,
            };
            var cast = Unsafe.As<UnsafeSegment<T>, ArraySegment<T>>(ref segment);
            Debug.Assert(cast.Array == array);
            Debug.Assert(cast.Offset == 1);
            Debug.Assert(cast.Count == 2);
        }
#endif
    }
}
