using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Extensions;
using FlameCsv.Reading.Unescaping;

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

    private int _metaCount;

    /// <summary>
    /// Realized fields.
    /// </summary>
    private Field[] _fields;

    /// <summary>
    /// List of realized records. The indexes point to <see cref="_fields"/>.
    /// </summary>
    private ListBuilder<FieldRange> _records; // don't make me readonly

    /// <summary>
    /// Index of the cursor in pre-parsed records.
    /// </summary>
    private int _recordIndex = 0;

    public MetaBuffer()
    {
        int capacity = FlameCsvGlobalOptions.ReadAheadCount;

        _array = ArrayPool<Meta>.Shared.Rent(capacity);
        _array[0] = Meta.StartOfData;

        _fields = ArrayPool<Field>.Shared.Rent(capacity);
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
        startIndex = _array[_metaCount].NextStart;
        return _array.AsSpan(start: _metaCount + 1);
    }

    /// <summary>
    /// Marks fields as read, and returns the end position of the last field.
    /// </summary>
    /// <param name="count"></param>
    public int SetFieldsRead(int count)
    {
        Debug.Assert(count >= 0);
        Debug.Assert((_metaCount + count) < _array.Length);
        _metaCount += count;
        return _array[_metaCount].NextStart;
    }

    /// <summary>
    /// This should be called to ensure massive records without a newline can fit in the buffer.
    /// </summary>
    /// <returns></returns>
    public void EnsureCapacity()
    {
        if (_metaCount >= (_array.Length * 15 / 16))
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
        if (_recordIndex == 0 || _metaCount == 0)
        {
            return 0;
        }

        return ResetCore();
    }

    private int GetConsumedMetaCount()
    {
        FieldRange lastRecord = _records.AsSpan()[_recordIndex - 1];
        return lastRecord.Start + lastRecord.Length;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int ResetCore()
    {
        Debug.Assert(_recordIndex > 0);

        // get the last record read, and get the end index of the final field in it.
        // we can fetch the original Meta struct from the array using the index (offset by 1)
        int index = GetConsumedMetaCount() + 1;

        Meta lastRead = _array[index];
        int offset = lastRead.NextStart;

        Span<Meta> buffer = _array.AsSpan(start: 1 + index, length: _metaCount - index);

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

        _metaCount -= index;

        Debug.Assert(_array[0] == Meta.StartOfData);
        Debug.Assert(lastRead.IsEOL);
        Debug.Assert(_metaCount >= 0);

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
                array = _fields,
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
        _metaCount = 0;
        ArrayPool<Meta>.Shared.EnsureCapacity(ref _array, FlameCsvGlobalOptions.ReadAheadCount);
        _array[0] = Meta.StartOfData;

        ArrayPool<Field>.Shared.EnsureCapacity(ref _fields, FlameCsvGlobalOptions.ReadAheadCount);

        _recordIndex = 0;
        _records.Reset();
    }

    public void Dispose()
    {
        _metaCount = 0;
        _recordIndex = 0;

        ArrayPool<Meta>.Shared.Return(_array);
        _array = [];

        ArrayPool<Field>.Shared.Return(_fields);
        _fields = [];

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

    private string DebuggerDisplay
    {
        get
        {
            if (_array.Length == 0)
            {
                return "{ Empty }";
            }

            int consumed = GetConsumedMetaCount();

            return $"{{ {_metaCount} read, {_metaCount - consumed} available, range: [{_array[consumed].NextStart}..{_array[_metaCount].NextStart}] }}";
        }
    }

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
        {
            get
            {
                Meta[] array = _buffer._array;

                if (array.Length == 0)
                {
                    return [];
                }

                return array.AsSpan(Math.Max(1, _buffer.GetConsumedMetaCount()), _buffer._metaCount).ToArray();
            }
        }
    }

    public void Process<T, TTrimmer>(ReadOnlySpan<T> data, T quote)
        where T : unmanaged, IBinaryInteger<T>
        where TTrimmer : struct, ITrimmer
    {
        int fieldCount = _metaCount;

        ref Meta meta = ref MemoryMarshal.GetArrayDataReference(_array);
        ref Field field = ref MemoryMarshal.GetArrayDataReference(_fields);
        ref T first = ref MemoryMarshal.GetReference(data);

        nint metaIndex = 1;
        nint fieldIndex = 0;
        nint recordStart = 0;

        int start = meta.NextStart;

        while (metaIndex <= fieldCount)
        {
            Meta current = Unsafe.Add(ref meta, metaIndex);
            int length = current.End - start;

            // Safety check for length
            if ((uint)length > ushort.MaxValue)
            {
                Throw.Argument("Field length exceeds maximum allowed", "");
            }

            int specialCount = (int)current.SpecialCount;
            bool isEscape = current.IsEscape;

            // Safety check for special count
            if (specialCount > Field.MaxSpecialCount)
            {
                // TODO: better exception
                Throw.Argument("Special count exceeds maximum allowed", "");
            }

            ReadOnlySpan<T> span = MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref first, start), length);
            TTrimmer.Trim(ref span);

            if (current.HasControlCharacters)
            {
                if (Unsafe.Add(ref first, start) != quote || Unsafe.Add(ref first, start + length - 1) != quote)
                {
                    IndexOfUnescaper.Invalid(span, in current);
                }

                start++;
                length -= 2;

                Unsafe.Add(ref field, fieldIndex) = new Field(
                    start,
                    (ushort)length,
                    (ushort)specialCount,
                    isEscape,
                    needsProcessing: isEscape || specialCount != 2
                );
            }
            else
            {
                Unsafe.Add(ref field, fieldIndex) = new Field(start, (ushort)length);
            }

            start = current.NextStart;
            metaIndex++;
            fieldIndex++;

            if (current.IsEOL)
            {
                _records.Push(new FieldRange((int)recordStart, (int)(fieldIndex - recordStart)));
                recordStart = fieldIndex;
            }
        }

        Debug.Assert(metaIndex <= _array.Length);
        Debug.Assert(fieldIndex <= _fields.Length);
    }
}
