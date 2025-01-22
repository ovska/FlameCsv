using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

internal static class CsvParser
{
    [MustDisposeResource]
    public static CsvParser<T> Create<T>(CsvOptions<T> options) where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();
        return !options.Dialect.Escape.HasValue
            ? new CsvParserRFC4180<T>(options)
            : new CsvParserUnix<T>(options);
    }
}

/// <summary>
/// Reads CSV records from a <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <remarks>Internal implementation detail.</remarks>
[MustDisposeResource]
internal abstract class CsvParser<T> : IDisposable where T : unmanaged, IBinaryInteger<T>
{
    public const int BufferedFields = 1024;

    /// <summary>
    /// Current options instance.
    /// </summary>
    public CsvOptions<T> Options => _options;

    /// <summary>
    /// Length of the newline sequence.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Options has no explicit newline configured, and the parser hasn't auto-detected a newline yet.
    /// </exception>
    public int NewlineLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_newline.Length == 0)
            {
                Throw.InvalidOperation("Auto-detected newline not initialized");
            }

            return _newline.Length;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Advance(ICsvPipeReader<T> pipeReader)
    {
        pipeReader.AdvanceTo(consumed: _sequence.Start, examined: _sequence.End);
    }

    internal MemoryPool<T> Allocator => _options._memoryPool;

    internal readonly CsvDialect<T> _dialect;
    internal NewlineBuffer<T> _newline;
    private protected readonly CsvOptions<T> _options;
    internal ReadOnlySequence<T> _sequence;

    private protected IMemoryOwner<T>? _multisegmentBuffer;
    internal IMemoryOwner<T>? _unescapeBuffer;

    /// <summary>
    /// Buffer to read fields into.
    /// </summary>
    protected Span<Meta> MetaBuffer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _metaArray.AsSpan(start: 1);
    }

    /// <summary>
    /// Array containing the field infos. The first index is reserved for the start-of-data item.
    /// </summary>
    /// <seealso cref="MetaBuffer"/>
    private Meta[] _metaArray;

    /// <summary>
    /// Number of fields read by <see cref="ReadFromFirstSpan"/>.
    /// </summary>
    /// <remarks>
    /// Does not include the start-of-data meta.
    /// </remarks>
    /// <seealso cref="_metaArray"/>
    private int _metaCount;

    /// <summary>
    /// How many read fields have been consumed.
    /// </summary>
    private int _metaIndex;

    /// <summary>
    /// Whether we have an ASCII dialect, and buffering isn't disabled.
    /// </summary>
    private readonly bool _canUseFastPath;

    private protected CsvParser(CsvOptions<T> options)
    {
        Debug.Assert(options.IsReadOnly);

        _options = options;
        _dialect = options.Dialect;
        _newline = options.Dialect.GetNewlineOrDefault();
        _metaArray = [];
        _canUseFastPath = !options.NoLineBuffering && _dialect.IsAscii;
    }

    /// <summary>
    /// Attempt to read a complete well-formed line (CSV record) from the reader.
    /// </summary>
    /// <param name="line">Memory containing the line.</param>
    /// <param name="isFinalBlock">Whether more data can be expected after this read</param>
    /// <returns>Number of fields parsed</returns>
    /// <seealso cref="_metaArray"/>
    private protected abstract bool TryReadFromSequence(out CsvLine<T> line, bool isFinalBlock);

    /// <summary>
    /// Read metas from the first segment.
    /// </summary>
    /// <returns>Number of fields parsed</returns>
    /// <seealso cref="_metaArray"/>
    private protected abstract int ReadFromFirstSpan();

    /// <inheritdoc/>
    public void Dispose()
    {
        using (_unescapeBuffer)
        using (_multisegmentBuffer)
        {
            _sequence = default;
            _multisegmentBuffer = null;
            ArrayPool<Meta>.Shared.Return(_metaArray);
            _metaArray = [];
        }

        _unescapeBuffer = null;
        _multisegmentBuffer = null;
    }

    /// <summary>
    /// Resets the data of the reader.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(in ReadOnlySequence<T> sequence)
    {
        _metaCount = 0;
        _metaIndex = 0;
        _sequence = sequence;
    }

    public Span<T> GetUnescapeBuffer(int length)
    {
        return Allocator.EnsureCapacity(ref _unescapeBuffer, length, copyOnResize: false).Span;
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
        if (_metaIndex < _metaCount)
        {
            if (Meta.TryFindNextEOL(_metaArray.AsSpan((1 + _metaIndex)..(_metaCount + 1)), out int fieldCount))
            {
                line = new CsvLine<T>(this, _sequence.First, _metaArray.AsSpan(_metaIndex, fieldCount + 1));
                _metaIndex += fieldCount;
                return true;
            }
        }

        return TryReadSlow(out line, isFinalBlock);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AdvanceAndResetMeta()
    {
        Debug.Assert(_canUseFastPath && _metaIndex != 0);
        Debug.Assert(_newline.Length != 0);
        Debug.Assert(_metaCount >= _metaIndex);

        var lastEOL = _metaArray[_metaIndex];

        Debug.Assert(lastEOL.IsEOL);

        _sequence = _sequence.Slice(lastEOL.GetNextStart(_newline.Length));
        _metaCount = 0;
        _metaIndex = 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryReadSlow(out CsvLine<T> line, bool isFinalBlock)
    {
        if (_newline.Length == 0 && !TryPeekNewline())
        {
            if (isFinalBlock)
                goto ReadFromSequence;

            goto Fail;
        }

        Debug.Assert(_newline.Length != 0, "TryPeekNewline should have initialized newline");

        if (_sequence.IsEmpty)
            goto Fail;

        if (_canUseFastPath && !isFinalBlock)
        {
            if (_metaIndex != 0)
            {
                AdvanceAndResetMeta();
            }

            // delay the rent until first read
            if (_metaArray.Length == 0)
            {
                _metaArray = ArrayPool<Meta>.Shared.Rent(BufferedFields);
                _metaArray[0] = Meta.StartOfData; // the first meta should be one delimiter "behind"
            }

            int fieldCount = ReadFromFirstSpan();

            // see if we read at least one fully formed line
            if (fieldCount != 0 && Meta.HasEOL(MetaBuffer[..fieldCount], out int lastIndex))
            {
                _metaIndex = 0;
                _metaCount = lastIndex + 1;
                return TryReadLine(out line, isFinalBlock);
            }
        }

    ReadFromSequence:
        return TryReadFromSequence(out line, isFinalBlock);

    Fail:
        Debug.Assert(_metaCount == 0);
        Unsafe.SkipInit(out line);
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SkipRecord(ReadOnlyMemory<T> record, int line, bool isHeader)
    {
        return _options._shouldSkipRow is { } predicate &&
            predicate(
                new CsvRecordSkipArgs<T>
                {
                    Options = _options,
                    Line = line,
                    Record = record.Span,
                    IsHeader = isHeader,
                });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool SkipRecord(ref readonly CsvLine<T> record, int line, bool isHeader)
    {
        return _options._shouldSkipRow is { } predicate &&
            predicate(
                new CsvRecordSkipArgs<T>
                {
                    Options = _options,
                    Line = line,
                    Record = record.Data.Span,
                    IsHeader = isHeader,
                });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ExceptionIsHandled(ref readonly CsvLine<T> record, int line, Exception exception)
    {
        return _options._exceptionHandler is { } handler &&
            handler(
                new CsvExceptionHandlerArgs<T>
                {
                    Options = _options,
                    Line = line,
                    Record = record.Data.Span,
                    Exception = exception,
                });
    }

    public const int MaxNewlineDetectionLength = 1024;

    /// <summary>
    /// Attempt to auto-detect newline from the data.
    /// </summary>
    /// <returns>True if the current sequence contained a CRLF or LF (checked in that order)</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected bool TryPeekNewline()
    {
        if (_newline.Length != 0)
        {
            Throw.Unreachable($"{nameof(TryPeekNewline)} called with newline length of {_newline.Length}");
        }

        if (_sequence.IsEmpty)
        {
            return false;
        }

        // optimistic fast path for the first line containing no quotes or escapes
        if (_sequence.FirstSpan.IndexOf(NewlineBuffer<T>.LF.First) is var linefeedIndex and not -1)
        {
            // data starts with LF?
            if (linefeedIndex == 0)
            {
                _newline = NewlineBuffer<T>.LF;
                return true;
            }

            // ensure there were no quotes or escapes between the start of the buffer and the linefeed
            ReadOnlySpan<T> untilLf = _sequence.FirstSpan.Slice(0, linefeedIndex);

            if (untilLf.IndexOfAny(_dialect.Quote, _dialect.Escape ?? _dialect.Quote) == -1)
            {
                // check if CR in the data
                int firstCR = untilLf.IndexOf(NewlineBuffer<T>.CRLF.First);

                if (firstCR == -1)
                {
                    _newline = NewlineBuffer<T>.LF;
                    return true;
                }

                if (firstCR == untilLf.Length - 1)
                {
                    _newline = NewlineBuffer<T>.CRLF;
                    return true;
                }
            }
        }

        ReadOnlySequence<T> copy = _sequence;

        // limit the amount of data we read to avoid reading the entire CSV
        if (_sequence.Length > MaxNewlineDetectionLength)
        {
            _sequence = _sequence.Slice(0, MaxNewlineDetectionLength);
        }

        NewlineBuffer<T> result = default;

        try
        {
            // find the first linefeed as both auto-detected newlines contain it
            _newline = NewlineBuffer<T>.LF;

            while (TryReadFromSequence(out var firstLine, false))
            {
                // found a non-empty line?
                if (!firstLine.Data.IsEmpty)
                {
                    result = firstLine.Data.Span[^1] == NewlineBuffer<T>.CRLF.First
                        ? NewlineBuffer<T>.CRLF
                        : NewlineBuffer<T>.LF;
                    return true;
                }
            }

            // no line found, reset to the original state
            result = default;

            // \n not found, throw if we've read up to our threshold already
            if (copy.Length >= MaxNewlineDetectionLength)
            {
                throw new CsvFormatException(
                    $"Could not auto-detect newline even after {copy.Length} characters (no valid CRLF or LF tokens found)");
            }

            // maybe the first segment was just too small, or contained a single line without a newline
            return false;
        }
        finally
        {
            _sequence = copy; // reset original state
            _newline = result; // set the detected newline, or default if not found
        }
    }

    private protected ReadOnlySpan<Meta> GetSegmentMeta(scoped ReadOnlySpan<Meta> fields)
    {
        Debug.Assert(fields.Length != 0);
        Debug.Assert(_metaIndex == _metaCount);

        ArrayPool<Meta>.Shared.EnsureCapacity(ref _metaArray, Math.Max(BufferedFields, fields.Length + 1));
        _metaArray[0] = Meta.StartOfData;
        fields.CopyTo(_metaArray.AsSpan(start: 1));
        return _metaArray.AsSpan(0, fields.Length + 1);
    }
}
