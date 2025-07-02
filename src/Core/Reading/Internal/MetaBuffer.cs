using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using JetBrains.Annotations;

namespace FlameCsv.Reading.Internal;

[DebuggerTypeProxy(typeof(MetaBufferDebugView))]
[DebuggerDisplay("{ToString(),nq}")]
[SkipLocalsInit]
internal sealed class MetaBuffer : IDisposable
{
    /// <summary>
    /// Storage for the field metadata.
    /// </summary>
    private Meta[] _array;

    /// <summary>
    /// Number of fields that have been consumed from the buffer.
    /// </summary>
    private int _index;

    /// <summary>
    /// Number of fields that have been parsed to the buffer.
    /// </summary>
    private int _count;

    public MetaBuffer()
    {
        _array = ArrayPool<Meta>.Shared.Rent(4096);
        _array[0] = Meta.StartOfData;
        _index = 0;
        _count = 0;
    }

    /// <summary>
    /// Returns unconsumed meta buffer. If 3 fields were left in the buffer on last reset,
    /// returns array length - 3.
    /// </summary>
    /// <param name="startIndex">Start index in the data after the first free field, or 0 if there are none</param>
    public Span<Meta> GetUnreadBuffer(out int startIndex)
    {
        ObjectDisposedException.ThrowIf(_array.Length == 0, this);
        startIndex = _array[_count].NextStart;
        return _array.AsSpan(start: _count + 1);
    }

    /// <summary>
    /// Marks fields as read, and returns the end position of the last field.
    /// </summary>
    /// <param name="count"></param>
    public int SetFieldsRead(int count)
    {
        Debug.Assert(count >= 0);
        Debug.Assert((_count + count) < _array.Length);
        _count += count;
        return _array[_count].NextStart;
    }

    /// <summary>
    /// This should be called to ensure massive records without a newline can fit in the buffer.
    /// </summary>
    /// <returns></returns>
    public void EnsureCapacity()
    {
        if (_count >= (_array.Length * 15 / 16))
        {
            ArrayPool<Meta>.Shared.Resize(ref _array, _array.Length * 2);
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
        Meta lastRead = _array[_index];
        int offset = lastRead.NextStart;

        Span<Meta> buffer = _array.AsSpan(start: 1 + _index, length: _count - _index);

        // Preserve the EOL flag while shifting only the end position
        // this is always called with a small number of records so not worth to unroll
        foreach (ref var meta in buffer)
        {
            // Preserve the EOL flag while shifting only the end position
            int eolFlag = meta._endAndEol & Meta.EOLMask;
            int shiftedEnd = meta._endAndEol - offset;
            Unsafe.AsRef(in meta._endAndEol) = shiftedEnd | eolFlag;

            Debug.Assert(shiftedEnd >= 0);
        }

        buffer.CopyTo(_array.AsSpan(1));

        _count -= _index;
        _index = 0;

        Debug.Assert(_array[0] == Meta.StartOfData);
        Debug.Assert(lastRead.IsEOL);
        Debug.Assert(_count >= 0);

        return offset;
    }

    /// <summary>
    /// Attempts to load the next record from the buffer.
    /// </summary>
    /// <param name="fields">Fields in the record</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out ArraySegment<Meta> fields)
    {
        Unsafe.SkipInit(out fields);

        // bit 7 in the 4th byte corresponds to the EOL flag (bit 31)
        const byte mask = 1 << 7;

        // Get reference to the 4th byte of each Meta struct (where bit 31 of the ulong lives)
        ref byte metaByte = ref Unsafe.Add(
            ref Unsafe.As<Meta, byte>(ref MemoryMarshal.GetArrayDataReference(_array)),
            ((uint)(_index + 1u) * sizeof(ulong)) + 3u // force zero extension
        );

        nint end = _count - _index;
        nint unrolledEnd = end - 4;
        nint pos = 0;
        bool found = false;

        while (pos < unrolledEnd)
        {
            // load the position first and add the constants to it first, 248 -> 216 bytes of code
            ref byte b0 = ref Unsafe.Add(ref metaByte, pos * sizeof(ulong));
            byte b1 = Unsafe.Add(ref b0, sizeof(ulong));
            byte b2 = Unsafe.Add(ref b0, sizeof(ulong) * 2);
            byte b3 = Unsafe.Add(ref b0, sizeof(ulong) * 3);

            if ((b0 & mask) != 0)
            {
                pos += 1;
                found = true;
                break;
            }
            if ((b1 & mask) != 0)
            {
                pos += 2;
                found = true;
                break;
            }
            if ((b2 & mask) != 0)
            {
                pos += 3;
                found = true;
                break;
            }
            if ((b3 & mask) != 0)
            {
                pos += 4;
                found = true;
                break;
            }

            pos += 4;
        }

        while (!found && pos < end)
        {
            if ((Unsafe.Add(ref metaByte, pos++ * sizeof(ulong)) & mask) != 0)
            {
                found = true;
                break;
            }
        }

        // ran out of data
        if (!found)
        {
            return false;
        }

        Unsafe.As<ArraySegment<Meta>, MetaSegment>(ref Unsafe.AsRef(in fields)) = new()
        {
            array = _array,
            count = (int)pos + 1,
            offset = _index,
        };

        _index += (int)pos;
        return true;
    }

    public void Initialize()
    {
        _index = 0;
        _count = 0;
        ArrayPool<Meta>.Shared.EnsureCapacity(ref _array, 4096);
        _array[0] = Meta.StartOfData;
    }

    public void Dispose()
    {
        _index = 0;
        _count = 0;

        Meta[] local = _array;
        _array = [];

        if (local.Length > 0)
        {
            ArrayPool<Meta>.Shared.Return(local);
        }
    }

    internal ref Meta[] UnsafeGetArrayRef() => ref _array;

    public override string ToString() =>
        _array.Length == 0
            ? "{ Empty }"
            : $"{{ {_count} read, {_count - _index} available, range: [{_array[_index].NextStart}..{_array[_count].NextStart}] }}";

    private class MetaBufferDebugView
    {
        private readonly MetaBuffer _buffer;

        public MetaBufferDebugView(MetaBuffer buffer)
        {
            _buffer = buffer;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public Meta[] Items =>
            _buffer._array.Length == 0
                ? []
                : _buffer._array.AsSpan(Math.Max(1, _buffer._index), _buffer._count).ToArray();
    }

    private struct MetaSegment
    {
        [UsedImplicitly]
        public Meta[]? array;

        [UsedImplicitly]
        public int offset;

        [UsedImplicitly]
        public int count;

#if DEBUG
        static MetaSegment()
        {
            if (Unsafe.SizeOf<MetaSegment>() != Unsafe.SizeOf<ArraySegment<Meta>>())
            {
                throw new InvalidOperationException("MetaSegment has unexpected size");
            }

            var array = new Meta[4];
            var segment = new MetaSegment
            {
                array = array,
                offset = 1,
                count = 2,
            };
            var cast = Unsafe.As<MetaSegment, ArraySegment<Meta>>(ref segment);
            Debug.Assert(cast.Array == array);
            Debug.Assert(cast.Offset == 1);
            Debug.Assert(cast.Count == 2);
        }
#endif
    }
}
