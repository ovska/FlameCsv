using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading.Internal;

[DebuggerTypeProxy(typeof(MetaBufferDebugView))]
[DebuggerDisplay("{DebuggerDisplay,nq}")]
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

    /// <summary>
    /// Realized fields.
    /// </summary>
    private ListBuilder<Field> _fields; // don't make me readonly

    /// <summary>
    /// List of realized records. The indexes point to <see cref="_fields"/>.
    /// </summary>
    private ListBuilder<FieldRange> _records; // don't make me readonly

    private int _recordIndex = 0;

    public MetaBuffer()
    {
        int capacity = FlameCsvGlobalOptions.ReadAheadCount;

        _array = ArrayPool<Meta>.Shared.Rent(capacity);
        _array[0] = Meta.StartOfData;
        _index = 0;
        _count = 0;

        _fields = new ListBuilder<Field>(capacity);
        _records = new ListBuilder<FieldRange>(capacity / 8);
        _recordIndex = 0;
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

        foreach (ref var meta in buffer)
        {
#if DEBUG
            Meta orig = meta;
#endif

            // Preserve the EOL flag while shifting only the end position
            int eolFlag = meta._endAndEol & Meta.EOLMask;
            Unsafe.AsRef(in meta._endAndEol) = (meta._endAndEol & ~Meta.EOLMask) - offset | eolFlag;

#if DEBUG
            Debug.Assert(meta != Meta.StartOfData);
            Debug.Assert(orig.End == (offset + meta.End));
            Debug.Assert(orig.IsEOL == meta.IsEOL);
#endif
        }

        buffer.CopyTo(_array.AsSpan(1));

        _count -= _index;
        _index = 0;

        Debug.Assert(_array[0] == Meta.StartOfData);
        Debug.Assert(lastRead.IsEOL);
        Debug.Assert(_count >= 0);

        _fields.Reset();
        _records.Reset();

        return offset;
    }

    /// <summary>
    /// Attempts to load the next record from the buffer.
    /// </summary>
    /// <param name="fields">Fields in the record</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out ArraySegment<Field> fields)
    {
        ReadOnlySpan<FieldRange> records = _records.AsSpan();

        if (_recordIndex < records.Length)
        {
            FieldRange range = records[_recordIndex++];
            fields = new FieldSegment
            {
                array = _fields.UnsafeGetArray(),
                offset = range.Start,
                count = range.Length,
            };
            return true;
        }

        fields = default;
        return false;
    }

    public void Initialize()
    {
        _index = 0;
        _count = 0;
        ArrayPool<Meta>.Shared.EnsureCapacity(ref _array, FlameCsvGlobalOptions.ReadAheadCount);
        _array[0] = Meta.StartOfData;

        _fields.Reset();
        _records.Reset();
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

        _fields.Dispose();
        _records.Dispose();
    }

    private readonly struct FieldRange
    {
        public int Start { get; }
        public int Length { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public FieldRange(int start, int length)
        {
            Start = start;
            Length = length;
        }
    }

    private struct FieldSegment
    {
        public Field[]? array;
        public int offset;
        public int count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ArraySegment<Field>(FieldSegment segment)
        {
            return Unsafe.As<FieldSegment, ArraySegment<Field>>(ref segment);
        }

#if DEBUG
        static FieldSegment()
        {
            if (Unsafe.SizeOf<FieldSegment>() != Unsafe.SizeOf<ArraySegment<Field>>())
            {
                throw new InvalidOperationException("MetaSegment has unexpected size");
            }

            var array = new Field[4];
            var segment = new FieldSegment
            {
                array = array,
                offset = 1,
                count = 2,
            };
            var cast = Unsafe.As<FieldSegment, ArraySegment<Field>>(ref segment);
            Debug.Assert(cast.Array == array);
            Debug.Assert(cast.Offset == 1);
            Debug.Assert(cast.Count == 2);
        }
#endif
    }

    internal ref Meta[] UnsafeGetArrayRef() => ref _array;

    private string DebuggerDisplay =>
        _array.Length == 0
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
        public Meta[] Items =>
            _buffer._array.Length == 0
                ? []
                : _buffer._array.AsSpan(Math.Max(1, _buffer._index), _buffer._count).ToArray();
    }
}
