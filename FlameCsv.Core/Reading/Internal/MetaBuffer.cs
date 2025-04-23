using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading.Internal;

[DebuggerTypeProxy(typeof(MetaBufferDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
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
        _array = ArrayPool<Meta>.Shared.Rent(FlameCsvGlobalOptions.ReadAheadCount);
        _array[0] = Meta.StartOfData;
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
        Meta.Shift(buffer, offset);
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
        // TODO PERF: cache previous field count and fall back to TryFindNextEOL?
        // still need to ensure no newlines in the record though

        if (_index < _count)
        {
            ref Meta metaRef = ref MemoryMarshal.GetArrayDataReference(_array);

            if (Meta.TryFindNextEOL(
                    first: ref Unsafe.Add(ref metaRef, _index + 1),
                    end: _count - _index,
                    index: out int fieldCount))
            {
                MetaSegment fieldMeta = new()
                {
                    array = _array,
                    count = fieldCount + 1,
                    offset = _index,
                };
                fields = Unsafe.As<MetaSegment, ArraySegment<Meta>>(ref fieldMeta);

                _index += fieldCount;
                return true;
            }
        }

        Unsafe.SkipInit(out fields);
        return false;
    }

    public void Initialize()
    {
        _index = 0;
        _count = 0;
        ArrayPool<Meta>.Shared.EnsureCapacity(ref _array, FlameCsvGlobalOptions.ReadAheadCount);
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

    // ReSharper disable NotAccessedField.Local
    private struct MetaSegment
    {
        public Meta[]? array;
        public int offset;
        public int count;

#if DEBUG
        static MetaSegment()
        {
            if (Unsafe.SizeOf<MetaSegment>() != Unsafe.SizeOf<ArraySegment<Meta>>())
            {
                throw new InvalidOperationException("MetaSegment has unexpected size");
            }

            var array = new Meta[4];
            var segment = new MetaSegment { array = array, offset = 1, count = 2 };
            var cast = Unsafe.As<MetaSegment, ArraySegment<Meta>>(ref segment);
            Debug.Assert(cast.Array == array);
            Debug.Assert(cast.Offset == 1);
            Debug.Assert(cast.Count == 2);
        }
#endif
    }
    // ReSharper restore NotAccessedField.Local

    internal ref Meta[] UnsafeGetArrayRef() => ref _array;

    private string DebuggerDisplay
        => _array.Length == 0
            ? "{ Empty }"
            : $"{{ {_count} read, {_count - _index} available, range: [{_array[_index].NextStart}..{_array[_count].NextStart}] }}";

    public override string ToString() => DebuggerDisplay;

    private class MetaBufferDebugView
    {
        private readonly MetaBuffer _buffer;

        public MetaBufferDebugView(MetaBuffer buffer)
        {
            _buffer = buffer;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public Meta[] Items
            => _buffer._array.Length == 0
                ? []
                : _buffer._array.AsSpan(Math.Max(1, _buffer._index), _buffer._count).ToArray();
    }
}
