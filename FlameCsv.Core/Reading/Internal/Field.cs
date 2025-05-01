using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace FlameCsv.Reading.Internal;

/// <summary>
/// Represents a field in the CSV data.
/// </summary>
[SkipLocalsInit]
internal readonly struct Field
{
    private readonly int _startAndFlag;
    private readonly int _length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Field(int start, int length)
    {
        _startAndFlag = start;
        _length = length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Field(int start, int length, bool needsProcessing)
    {
        _startAndFlag = start | (needsProcessing ? Flag : 0);
        _length = length;
    }

    private const int Flag = unchecked((int)0x80000000);

    /// <summary>
    /// Returns the start position of the field in the source data,
    /// or the start position of the field after trimming wrapping quotes if <see cref="NeedsProcessing"/> is <c>false</c>.
    /// </summary>
    public int Start
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _startAndFlag & ~Flag;
    }

    /// <summary>
    /// Returns the length of the field,
    /// or length of the field after trimming wrapping quotes if <see cref="NeedsProcessing"/> is <c>false</c>.
    /// </summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length;
    }

    /// <summary>
    /// Returns <c>true</c> if the field needs processing.
    /// The field can be constructed from the source data directly if returns <c>false</c>.
    /// </summary>
    public bool NeedsProcessing
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (_startAndFlag & Flag) != 0;
    }
}

[SkipLocalsInit]
internal readonly struct FieldMetadata
{
    public required int Start { get; init; }
    public required int Length { get; init; }
    public int QuoteCount { get; init; }
    public int EscapeCount { get; init; }

    public bool NeedsUnescaping
    {
        // needs unescaping if quote count is not 0 or 2, or there are escapes
        get => ((QuoteCount & ~2) | EscapeCount) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Field AsField()
    {
        return new Field(Start, Length, NeedsUnescaping);
    }
}

[SkipLocalsInit]
internal struct ListBuilder<T> : IDisposable
    where T : struct
{
    private T[] _array;
    private int _count;

    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ListBuilder(int capacity = 512)
    {
        _array = ArrayPool<T>.Shared.Rent(capacity);
        _count = 0;
    }

    /// <summary>
    /// Allocates <paramref name="capacity"/> elements in the array and returns a span to write to.
    /// </summary>
    public Span<T> ResetAndGetCapacitySpan(int capacity)
    {
        if ((uint)capacity > (uint)_array.Length)
        {
            int newSize = Math.Max(_array.Length * 2, capacity);
            ArrayPool<T>.Shared.Resize(ref _array, newSize);
        }

        return new Span<T>(_array, 0, capacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset() => _count = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<T> AsSpan() => MemoryMarshal.CreateReadOnlySpan(ref _array[0], _count);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T UnsafeGetRef(out int count)
    {
        count = _count;
        return ref MemoryMarshal.GetArrayDataReference(_array);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T item)
    {
        if ((uint)_count >= (uint)_array.Length)
        {
            PushWithResize(item);
        }
        else
        {
            _array[_count++] = item;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void PushWithResize(T item)
    {
        int newSize = Math.Max(_array.Length * 2, _count + 1);
        ArrayPool<T>.Shared.Resize(ref _array, newSize);
        _array[_count++] = item;
    }

    public void Dispose()
    {
        if (_array.Length > 0)
        {
            ArrayPool<T>.Shared.Return(_array);
            _array = [];
        }
    }
}

internal sealed class FieldStack : IDisposable
{
    private ListBuilder<Range> _records;
    private ListBuilder<Field> _fields;
    private ListBuilder<FieldMetadata> _fieldMetas;

    private int _currentStart;
    private int _currentLength;

    public FieldStack(int capacity = 512)
    {
        _fields = new ListBuilder<Field>(capacity);
        _fieldMetas = new ListBuilder<FieldMetadata>(capacity);
    }

    public void Reset()
    {
        _fields.Reset();
        _fieldMetas.Reset();
        _records.Reset();
        _currentStart = 0;
        _currentLength = 0;
    }

    public ReadOnlySpan<Range> Records
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _records.AsSpan();
    }

    public ReadOnlySpan<Field> Fields
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fields.AsSpan();
    }

    public ReadOnlySpan<FieldMetadata> FieldMetas
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _fieldMetas.AsSpan();
    }

    public void Process(ReadOnlySpan<Meta> fields)
    {
        if (fields.IsEmpty)
            return;

        int fieldCount = fields.Length - 1;

        Span<Field> fieldSpan = _fields.ResetAndGetCapacitySpan(fieldCount);
        Span<FieldMetadata> fieldMetaSpan = _fieldMetas.ResetAndGetCapacitySpan(fieldCount);

        ref Meta meta = ref MemoryMarshal.GetReference(fields);
        ref Field field = ref MemoryMarshal.GetReference(fieldSpan);
        ref FieldMetadata fieldMeta = ref MemoryMarshal.GetReference(fieldMetaSpan);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(FieldMetadata fieldMeta, bool isEOL)
    {
        _fields.Push(fieldMeta.AsField());
        _fieldMetas.Push(fieldMeta);

        if (isEOL)
        {
            _records.Push(new Range(_currentStart, _currentLength));
            _currentStart += fieldMeta.Start;
            _currentLength = 0;
        }
        else
        {
            _currentLength += fieldMeta.Length;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        _fields.Dispose();
        _fieldMetas.Dispose();
        _records.Dispose();
    }
}
