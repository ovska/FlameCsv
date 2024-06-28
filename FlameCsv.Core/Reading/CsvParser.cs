using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;

namespace FlameCsv.Reading;

public abstract class CsvParser : IDisposable
{
    internal protected readonly struct Slice
    {
        public int Index { get; init; }
        public int Length { get; init; }
        public CsvRecordMeta Meta { get; init; }
    }

    public abstract void Dispose();
}

public abstract class CsvParser<T> : CsvParser where T : unmanaged, IEquatable<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CsvParser<T> Create(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.MakeReadOnly();

        return !options._escape.HasValue
            ? new CsvParserRFC4180<T>(options)
            : new CsvParserUnix<T>(options);
    }

    public static CsvRecordMeta GetRecordMeta(ReadOnlyMemory<T> line, CsvOptions<T> options)
    {
        return !options._escape.HasValue
            ? CsvParserRFC4180<T>.GetRecordMeta(line, options)
            : CsvParserUnix<T>.GetRecordMeta(line, options);
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

    internal readonly T _quote;
    internal readonly T _newline1;
    internal readonly T _newline2;
    internal readonly int _newlineLength;

    internal protected readonly CsvOptions<T> _options;
    internal readonly ArrayPool<T> _arrayPool;
    protected T[]? _multisegmentBuffer;
    internal CsvSequenceReader<T> _reader;

    private readonly bool _noBuffering;
    private ReadOnlyMemory<T> _sliceBuffer;
    private int _sliceCount;
    private int _sliceIndex;
    private T[] _slices;

    private const int SliceBufferSize =
#if DEBUG
        32;
#else
        128;
#endif

    protected CsvParser(CsvOptions<T> options)
    {
        Debug.Assert(options.IsReadOnly);

        _options = options;
        _reader = new CsvSequenceReader<T>();
        _noBuffering = options.NoLineBuffering;
        _arrayPool = options._arrayPool.AllocatingIfNull();

        if (!_noBuffering)
        {
            // use user configured arraypool
            _slices = _arrayPool.Rent(Unsafe.SizeOf<Slice>() * SliceBufferSize / Unsafe.SizeOf<T>());
        }
        else
        {
            _slices = [];
        }

        _quote = options._quote;
        options.GetNewline(out _newline1, out _newline2, out _newlineLength);
    }

    [SuppressMessage("Usage", "CA1816:Dispose methods should call SuppressFinalize", Justification = "<Pending>")]
    public override void Dispose()
    {
        _arrayPool.EnsureReturned(ref _multisegmentBuffer);

        _sliceBuffer = null;
        _reader = default;
        _sliceCount = default;
        _sliceIndex = default;

        if (!_noBuffering)
        {
            _arrayPool.Return(_slices);
            _slices = [];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset(in ReadOnlySequence<T> sequence)
    {
        _sliceCount = 0;
        _sliceIndex = 0;
        _sliceBuffer = default;
        _reader = new(sequence);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadLine(
        out ReadOnlyMemory<T> line,
        out CsvRecordMeta meta,
        bool isFinalBlock)
    {
        if (_sliceCount > _sliceIndex)
        {
            Debug.Assert(_sliceIndex < _slices.AsSpan().Cast<T, Slice>().Length);

            ref Slice slice = ref Unsafe.Add(
                ref Unsafe.As<T, Slice>(ref MemoryMarshal.GetArrayDataReference(_slices)),
                _sliceIndex);

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
        if (_newlineLength == 0 && !TryPeekNewline())
        {
            if (isFinalBlock)
                goto ConsumeFinalBlock;

            goto Fail;
        }

        Debug.Assert(_newlineLength != 0, "TryPeekNewline should have initialized newline");

        if (_reader.End)
            goto Fail;

        if (_slices.Length > 0)
        {
            ReadOnlyMemory<T> unread = _reader.Unread;
            scoped Span<Slice> slices = _slices.AsSpan().Cast<T, Slice>();
            var (consumed, linesRead) = FillSliceBuffer(unread.Span, slices);

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
        line = _reader.UnreadSequence.AsMemory(ref _multisegmentBuffer, _arrayPool);
        meta = GetRecordMeta(line);
        _reader.AdvanceToEnd();
        return true;

        Fail:
        Debug.Assert(_sliceIndex == 0);
        Unsafe.SkipInit(out line);
        Unsafe.SkipInit(out meta);
        return false;
    }

    public abstract CsvRecordMeta GetRecordMeta(ReadOnlyMemory<T> line);
    protected abstract bool TryReadLine(out ReadOnlyMemory<T> line, out CsvRecordMeta meta);
    protected abstract (int consumed, int linesRead) FillSliceBuffer(ReadOnlySpan<T> data, scoped Span<Slice> slices);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SkipRecord(ReadOnlyMemory<T> record, int line, bool? headerRead)
    {
        return _options._shouldSkipRow is { } predicate && predicate(new CsvRecordSkipArgs<T>
        {
            Options = _options,
            Line = line,
            Record = record,
            HeaderRead = headerRead,
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ExceptionIsHandled(ReadOnlyMemory<T> record, int line, Exception exception)
    {
        return _options._exceptionHandler is { } handler && handler(new CsvExceptionHandlerArgs<T>
        {
            Options = _options,
            Line = line,
            Record = record,
            Exception = exception,
        });
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected static void ThrowForInvalidLastEscape(ReadOnlyMemory<T> line, CsvOptions<T> options)
    {
        throw new CsvFormatException($"The record ended with an escape character: {options.AsPrintableString(line)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected static void ThrowForInvalidEscapeQuotes(ReadOnlyMemory<T> line, CsvOptions<T> options)
    {
        throw new CsvFormatException(
            $"The entry had an invalid amount of quotes for escaped CSV: {options.AsPrintableString(line)}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected static void ThrowForUnevenQuotes(ReadOnlyMemory<T> line, CsvOptions<T> options)
    {
        throw new ArgumentException($"The data had an uneven amount of quotes: {options.AsPrintableString(line)}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected bool TryPeekNewline()
    {
        Debug.Assert(_newlineLength == 0, $"TryPeekNewline called with invalid newline length: {_newlineLength}");

        Debug.Assert(
            (typeof(T) == typeof(char) && _newline1.Equals((T)(object)'\r') && _newline2.Equals((T)(object)'\n')) ||
            (typeof(T) == typeof(byte) && _newline1.Equals((T)(object)(byte)'\r') && _newline2.Equals((T)(object)(byte)'\n')),
            $"Invalid default newline for {typeof(T).FullName}: [{_newline1}, {_newline2}]");

        CsvSequenceReader<T> copy = _reader;
        int foundNewlineLength = 0;
        T originalNewline1 = _newline1;

        try
        {
            Unsafe.AsRef(in _newlineLength) = 2;

            if (!TryReadLine(out _, out _))
            {
                Unsafe.AsRef(in _newlineLength) = 1;
                Unsafe.AsRef(in _newline1) = _newline2;

                _reader = copy;

                if (!TryReadLine(out _, out _))
                {
                    return false;
                }

                foundNewlineLength = 1;
            }
            else
            {
                foundNewlineLength = 2;
            }
        }
        finally
        {
            // reset original state, we are only peeking
            _reader = copy;
            Unsafe.AsRef(in _newlineLength) = 0;
            Unsafe.AsRef(in _newline1) = originalNewline1;
        }

        // it's impossible to get to this point using \r as escape/quote, see CsvOptions.ValidateDialect()
        // we can be certain that a line ending in \n is valid, and possibly contained the preceding \r
        if (foundNewlineLength == 2)
        {
            // crlf
            Unsafe.AsRef(in _newlineLength) = 2;
        }
        else if (foundNewlineLength == 1)
        {
            // lf
            Unsafe.AsRef(in _newline1) = _newline2;
            Unsafe.AsRef(in _newlineLength) = 1;
        }
        else
        {
            throw new UnreachableException($"Reached end of TryPeekNewline with length {foundNewlineLength}");
        }

        return true;
    }
}
