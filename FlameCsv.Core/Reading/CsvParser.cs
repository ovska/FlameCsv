using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

public abstract class CsvParser : IDisposable
{
    protected readonly struct Slice
    {
        public int Index { get; init; }
        public int Length { get; init; }
        public CsvRecordMeta Meta { get; init; }
    }

    public abstract void Dispose();

    protected const int SliceBufferSize =
#if DEBUG
        32;
#else
    128;
#endif

    [InlineArray(SliceBufferSize)]
    protected struct SliceBuffer
    {
        public Slice elem0;
    }
}

[MustDisposeResource]
public abstract class CsvParser<T> : CsvParser where T : unmanaged, IBinaryInteger<T>
{
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

    public CsvOptions<T> Options => _options;
    public bool HasHeader => _options._hasHeader;

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

    protected internal readonly CsvOptions<T> _options;
    protected IMemoryOwner<T>? _multisegmentBuffer;
    internal CsvSequenceReader<T> _reader;

    private readonly bool _noBuffering;
    private ReadOnlyMemory<T> _sliceBuffer;
    private int _sliceCount;
    private int _sliceIndex;
    private SliceBuffer _slices;

    protected CsvParser(CsvOptions<T> options)
    {
        Debug.Assert(options.IsReadOnly);

        _options = options;
        _reader = new CsvSequenceReader<T>();
        _noBuffering = options.NoLineBuffering;
        _quote = options.Dialect.Quote;
        _newline = options.GetNewline();
    }

    public abstract CsvRecordMeta GetRecordMeta(ReadOnlySpan<T> line);
    protected abstract bool TryReadLine(out ReadOnlyMemory<T> line, out CsvRecordMeta meta);
    protected abstract (int consumed, int linesRead) FillSliceBuffer(ReadOnlySpan<T> data, scoped Span<Slice> slices);

    [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>")]
    public override void Dispose()
    {
        _multisegmentBuffer?.Dispose();
        _multisegmentBuffer = null;
        _reader = default;
        _sliceBuffer = default;
        _slices = default;
        _sliceCount = default;
        _sliceIndex = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(in ReadOnlySequence<T> sequence)
    {
        _sliceCount = 0;
        _sliceIndex = 0;
        _sliceBuffer = default; // don't hold on to Memory instances from previous reads
        _reader = new CsvSequenceReader<T>(in sequence);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadLine(
        out ReadOnlyMemory<T> line,
        out CsvRecordMeta meta,
        bool isFinalBlock)
    {
        if (_sliceCount > _sliceIndex)
        {
            Debug.Assert(_sliceIndex < SliceBufferSize);

            ref Slice slice = ref _slices[_sliceIndex];

            line = _sliceBuffer.Slice(slice.Index, slice.Length);
            meta = slice.Meta;

            if (++_sliceIndex >= _sliceCount)
            {
                _sliceBuffer = default;
                _sliceCount = 0;
                _sliceIndex = 0;
            }

            return true;
        }

        return TryReadSlow(out line, out meta, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadSlow(out ReadOnlyMemory<T> line, out CsvRecordMeta meta, bool isFinalBlock)
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
                return TryReadLine(out line, out meta, isFinalBlock);
            }
        }

        if (!isFinalBlock)
        {
            return TryReadLine(out line, out meta);
        }

    ConsumeFinalBlock:
        Debug.Assert(isFinalBlock);
        line = _reader.UnreadSequence.AsMemory(Allocator, ref _multisegmentBuffer);
        meta = GetRecordMeta(line.Span);
        _reader.AdvanceToEnd();
        return true;

    Fail:
        Debug.Assert(_sliceIndex == 0);
        Unsafe.SkipInit(out line);
        Unsafe.SkipInit(out meta);
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SkipRecord(ReadOnlyMemory<T> record, int line, bool isHeader)
    {
        return _options._shouldSkipRow is { } predicate
            && predicate(
                new CsvRecordSkipArgs<T>
                {
                    Options = _options, Line = line, Record = record.Span, IsHeader = isHeader,
                });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SkipRecord(ReadOnlySpan<T> record, int line, bool isHeader)
    {
        return _options._shouldSkipRow is { } predicate
            && predicate(
                new CsvRecordSkipArgs<T>
                {
                    Options = _options, Line = line, Record = record, IsHeader = isHeader,
                });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ExceptionIsHandled(ReadOnlySpan<T> record, int line, Exception exception)
    {
        return _options._exceptionHandler is { } handler
            && handler(
                new CsvExceptionHandlerArgs<T>
                {
                    Options = _options, Line = line, Record = record, Exception = exception,
                });
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected static void ThrowForInvalidLastEscape(ReadOnlySpan<T> line, CsvOptions<T> options)
    {
        throw new CsvFormatException($"The record ended with an escape character: {options.AsPrintableString(line)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected static void ThrowForInvalidEscapeQuotes(ReadOnlySpan<T> line, CsvOptions<T> options)
    {
        throw new CsvFormatException(
            $"The entry had an invalid amount of quotes for escaped CSV: {options.AsPrintableString(line)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected static void ThrowForUnevenQuotes(ReadOnlySpan<T> line, CsvOptions<T> options)
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
            if (!TryReadLine(out _, out _))
            {
                // reset reader
                _reader = copy;
                _newline =  NewlineBuffer<T>.LF;

                // \r\n not found, perhaps just \n used?
                if (!TryReadLine(out _, out _))
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
