using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Readers.Internal;
using FlameCsv.Runtime;

namespace FlameCsv.Readers;

internal readonly struct CsvProcessor<T, TValue> : ICsvProcessor<T, TValue>
    where T : unmanaged, IEquatable<T>
{
    private static readonly int StackallocThreshold = Unsafe.SizeOf<byte>() * 256 / Unsafe.SizeOf<T>();

    /// <summary>Shared buffer for string unescaping in the data.</summary>
    private readonly BufferOwner<T> _enumeratorBuffer;

    /// <summary>Shared buffer for long segment-fragmented lines.</summary>
    private readonly BufferOwner<T> _multisegmentBuffer;

    private readonly CsvParserOptions<T> _options;
    private readonly CsvCallback<T, bool>? _skipPredicate;
    private readonly ICsvRowState<T, TValue> _state;
    private readonly int _columnCount;

    public CsvProcessor(CsvConfiguration<T> configuration, ICsvRowState<T, TValue>? state = null)
    {
        _options = configuration.Options;
        _skipPredicate = configuration.ShouldSkipRow;
        _state = state ?? configuration.BindToState<TValue>();
        _columnCount = _state.ColumnCount;

        // Two buffers are needed, as the ReadOnlySpan being manipulated by string escaping in the enumerator
        // might originate from the multisegment buffer
        _enumeratorBuffer = new(configuration.Security);
        _multisegmentBuffer = new(configuration.Security);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryContinueRead(ref ReadOnlySequence<T> buffer, out TValue value)
    {
        if (LineReader.TryRead(
                in _options,
                ref buffer,
                out ReadOnlySequence<T> line,
                out int stringDelimiterCount))
        {
            return TryReadColumns(in line, stringDelimiterCount, out value);
        }

        Unsafe.SkipInit(out value);
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadColumns(
        in ReadOnlySequence<T> line,
        int stringDelimiterCount,
        out TValue value)
    {
        if (line.IsSingleSegment)
        {
            return TryReadColumnSpan(line.FirstSpan, stringDelimiterCount, out value);
        }

        int length = (int)line.Length;

        if (length <= StackallocThreshold)
        {
            Span<T> buffer = stackalloc T[length];
            line.CopyTo(buffer);
            return TryReadColumnSpan(buffer, stringDelimiterCount, out value);
        }
        else
        {
            Span<T> buffer = _multisegmentBuffer.GetSpan(length)[..length];
            line.CopyTo(buffer);
            return TryReadColumnSpan(buffer, stringDelimiterCount, out value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadColumnSpan(
        ReadOnlySpan<T> line,
        int stringDelimiterCount,
        out TValue value)
    {
        if (_skipPredicate is not null && _skipPredicate(line, in _options))
        {
            Unsafe.SkipInit(out value);
            return false;
        }

        var enumerator = new CsvColumnEnumerator<T>(
            line,
            in _options,
            _columnCount,
            stringDelimiterCount,
            _enumeratorBuffer);

        value = _state.Parse(ref enumerator);
        return true;
    }

    public void Dispose()
    {
        _state.Dispose();
        _enumeratorBuffer.Dispose();
        _multisegmentBuffer.Dispose();
    }
}
