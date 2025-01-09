using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Implementation detail.
/// </summary>
public abstract class CsvParser : IDisposable
{
    /// <summary>
    /// Used to cache lines read from the first memory of the current sequence.
    /// </summary>
    private protected readonly struct Slice
    {
        public required int Index { get; init; }
        public required int Length { get; init; }
        public required uint QuoteCount { get; init; }
        public uint EscapeCount { get; init; }
    }

    /// <inheritdoc/>
    public abstract void Dispose();

    [InlineArray(
#if DEBUG
        32
#else
        128
#endif
    )]
    private protected struct SliceBuffer
    {
        public Slice elem0;
    }
}

/// <summary>
/// Reads CSV records from a <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <remarks>Internal implementation detail.</remarks>
[MustDisposeResource] // TODO: see if this can be internal?
public abstract class CsvParser<T> : CsvParser where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Creates a new instance of <see cref="CsvParser{T}"/> for the specified options.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MustDisposeResource]
    public static CsvParser<T> Create(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();
        return !options.Dialect.Escape.HasValue
            ? new CsvParserRFC4180<T>(options)
            : new CsvParserUnix<T>(options);
    }

    /// <summary>
    /// Current options instance.
    /// </summary>
    public CsvOptions<T> Options => _options;

    /// <summary>
    /// Whether the parser has reached the end of the current data.
    /// </summary>
    public bool End
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.End;
    }

    internal void Advance(ICsvPipeReader<T> pipeReader)
    {
        pipeReader.AdvanceTo(consumed: _reader.Position, examined: _reader.Sequence.End);
    }

    internal MemoryPool<T> Allocator => _options._memoryPool;

    internal readonly T _quote;
    internal NewlineBuffer<T> _newline;

    private protected readonly CsvOptions<T> _options;
    private protected  IMemoryOwner<T>? _multisegmentBuffer;
    internal CsvSequenceReader<T> _reader;

    private readonly bool _noBuffering;
    private ReadOnlyMemory<T> _sliceBuffer;
    private int _sliceCount;
    private int _sliceIndex;
    private SliceBuffer _slices;

    private protected  CsvParser(CsvOptions<T> options)
    {
        Debug.Assert(options.IsReadOnly);

        _options = options;
        _reader = new CsvSequenceReader<T>();
        _noBuffering = options.NoLineBuffering;
        _quote = options.Dialect.Quote;
        _newline = options.GetNewline();
    }

    internal abstract CsvLine<T> GetAsCsvLine(ReadOnlyMemory<T> line);
    private protected  abstract bool TryReadLine(out CsvLine<T> line);
    private protected  abstract (int consumed, int linesRead) FillSliceBuffer(ReadOnlySpan<T> data, scoped Span<Slice> slices);

    /// <inheritdoc/>
    public override void Dispose()
    {
        GC.SuppressFinalize(this);
        _multisegmentBuffer?.Dispose();
        _multisegmentBuffer = null;
        _reader = default;
        _sliceBuffer = default;
        _slices = default;
        _sliceCount = default;
        _sliceIndex = default;
    }

    /// <summary>
    /// Resets the data of the reader.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(in ReadOnlySequence<T> sequence)
    {
        _sliceCount = 0;
        _sliceIndex = 0;
        _sliceBuffer = default; // don't hold on to Memory instances from previous reads
        _reader = new CsvSequenceReader<T>(in sequence);
    }

    /// <summary>
    /// Attempts to read a complete well-formed line (CSV record) from the underlying data.
    /// </summary>
    /// <param name="line">CSV record</param>
    /// <param name="isFinalBlock">Whether no more data is possible to read</param>
    /// <returns>True if a record was read</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadLine(out CsvLine<T> line, bool isFinalBlock)
    {
        if (_sliceCount > _sliceIndex)
        {
            ref Slice slice = ref _slices[_sliceIndex];

            line = new()
            {
                Value = _sliceBuffer.Slice(slice.Index, slice.Length),
                QuoteCount = slice.QuoteCount,
                EscapeCount = slice.EscapeCount,
            };

            if (++_sliceIndex >= _sliceCount)
            {
                _sliceBuffer = default;
                _sliceCount = 0;
                _sliceIndex = 0;
            }

            return true;
        }

        return TryReadSlow(out line, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadSlow(out CsvLine<T> line, bool isFinalBlock)
    {
        if (_newline.Length == 0 && !TryPeekNewline())
        {
            if (isFinalBlock)
                goto ConsumeFinalBlock;

            goto Fail;
        }

        Debug.Assert(_newline.Length != 0, "TryPeekNewline should have initialized newline");

        if (_reader.End)
            goto Fail;

        if (!_noBuffering)
        {
            ReadOnlyMemory<T> unread = _reader.Unread;
            (int consumed, int linesRead) = FillSliceBuffer(unread.Span, _slices);

            if (linesRead > 0)
            {
                _sliceIndex = 0;
                _sliceCount = linesRead;
                _sliceBuffer = unread;
                _reader.AdvanceCurrent(consumed);
                return TryReadLine(out line, isFinalBlock);
            }
        }

        if (!isFinalBlock)
        {
            return TryReadLine(out line);
        }

    ConsumeFinalBlock:
        Debug.Assert(isFinalBlock);
        line = GetAsCsvLine(_reader.UnreadSequence.AsMemory(Allocator, ref _multisegmentBuffer));
        _reader.AdvanceToEnd();
        return true;

    Fail:
        Debug.Assert(_sliceIndex == 0);
        Unsafe.SkipInit(out line);
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SkipRecord(ReadOnlyMemory<T> record, int line, bool isHeader)
    {
        return _options._shouldSkipRow is { } predicate
            && predicate(
                new CsvRecordSkipArgs<T>
                {
                    Options = _options, Line = line, Record = record.Span, IsHeader = isHeader,
                });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SkipRecord(ref readonly CsvLine<T> record, int line, bool isHeader)
    {
        return _options._shouldSkipRow is { } predicate
            && predicate(
                new CsvRecordSkipArgs<T>
                {
                    Options = _options, Line = line, Record = record.Value.Span, IsHeader = isHeader,
                });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ExceptionIsHandled(ref readonly CsvLine<T> record, int line, Exception exception)
    {
        return _options._exceptionHandler is { } handler
            && handler(
                new CsvExceptionHandlerArgs<T>
                {
                    Options = _options, Line = line, Record = record.Value.Span, Exception = exception,
                });
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private protected static void ThrowForInvalidLastEscape(ReadOnlySpan<T> line, CsvOptions<T> options)
    {
        throw new CsvFormatException($"The record ended with an escape character: {options.AsPrintableString(line)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private protected static void ThrowForInvalidEscapeQuotes(ReadOnlySpan<T> line, CsvOptions<T> options)
    {
        throw new CsvFormatException(
            $"The entry had an invalid amount of quotes for escaped CSV: {options.AsPrintableString(line)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private protected static void ThrowForUnevenQuotes(ReadOnlySpan<T> line, CsvOptions<T> options)
    {
        throw new ArgumentException($"The data had an uneven amount of quotes: {options.AsPrintableString(line)}");
    }

    /// <summary>
    /// Attempt to auto-detect newline from the data.
    /// </summary>
    /// <returns>True if the current sequence contained a CRLF or LF (checked in that order)</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected bool TryPeekNewline()
    {
        Debug.Assert(_newline.Length == 0, $"TryPeekNewline called with invalid newline length: {_newline.Length}");

        CsvSequenceReader<T> copy = _reader;
        int foundNewlineLength;

        try
        {
            _newline = NewlineBuffer<T>.CRLF;

            // try to read \r\n
            if (!TryReadLine(out _))
            {
                // reset reader
                _reader = copy;
                _newline = NewlineBuffer<T>.LF;

                // \r\n not found, perhaps just \n used?
                if (!TryReadLine(out _))
                {
                    // found neither, throw if we've read a large chunk already
                    if (copy.Length > 1024L)
                    {
                        throw new CsvConfigurationException(
                            $"Could not auto-detect newline even after {copy.Length} characters (no valid CRLF or LF tokens found)");
                    }

                    // maybe the first segment was just too small, or contained a single line without a newline
                    return false;
                }

                foundNewlineLength = 1;
            }
            else
            {
                // unlikely that CSV contains any \r\n sequences unless it's a newline
                foundNewlineLength = 2;
            }
        }
        finally
        {
            // reset original state, we are only peeking
            _reader = copy;
            _newline = default;
        }

        // it's impossible to get to this point using \r as escape/quote, see CsvOptions.Dialect.Validate()
        // we can be certain that a line ending in \n is valid, and possibly contained the preceding \r
        if (foundNewlineLength == 2)
        {
            _newline = NewlineBuffer<T>.CRLF;
        }
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        else if (foundNewlineLength == 1)
        {
            _newline = NewlineBuffer<T>.LF;
        }
        else
        {
            throw new UnreachableException($"Reached end of TryPeekNewline with length {foundNewlineLength}");
        }

        return true;
    }
}
