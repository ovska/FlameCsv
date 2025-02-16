using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Provides a factory method for creating <see cref="CsvParser{T}"/> instances.
/// </summary>
public abstract class CsvParser
{
    // TODO: profile and adjust
    private protected const int BufferedFields = 4096;

    [ThreadStatic] private static Meta[]? StaticMetaBuffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static Meta[] GetMetaBuffer()
    {
        Meta[] array = StaticMetaBuffer ?? new Meta[BufferedFields];
        StaticMetaBuffer = null;
        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected static void ReturnMetaBuffer(ref Meta[] array)
    {
        // return the buffer to the thread-static unless someone read over a thousand fields into it
        if (array.Length == BufferedFields)
        {
            StaticMetaBuffer ??= array;
        }
    }

    /// <summary>
    /// Creates a new instance of a CSV parser.
    /// </summary>
    /// <param name="options">Options-instance that determines the dialect and memory pool to use</param>
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
/// <remarks>
/// Internal implementation detail, this type should not be used directly unless you know what you are doing.
/// </remarks>
[MustDisposeResource]
public abstract class CsvParser<T> : CsvParser, IDisposable where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Current options instance.
    /// </summary>
    public CsvOptions<T> Options { get; }

    internal readonly CsvDialect<T> _dialect;
    internal NewlineBuffer<T> _newline;
    internal ReadOnlySequence<T> _sequence;

    private protected IMemoryOwner<T>? _multisegmentBuffer;
    private IMemoryOwner<T>? _unescapeBuffer;

    /// <summary>
    /// Whether the instance has been disposed.
    /// </summary>
    protected bool IsDisposed { get; private set; }

    /// <summary>
    /// Buffer to read fields into.
    /// </summary>
    private protected Span<Meta> MetaBuffer
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
    /// The memory that the meta fields are based on.
    /// </summary>
    private ReadOnlyMemory<T> _metaMemory;

    /// <summary>
    /// Whether we have an ASCII dialect, and buffering isn't disabled.
    /// </summary>
    private readonly bool _canUseFastPath;

    private protected CsvParser(CsvOptions<T> options)
    {
        Debug.Assert(options.IsReadOnly);

        Options = options;
        _dialect = options.Dialect;
        _newline = options.Dialect.GetNewlineOrDefault();
        _metaArray = [];
        _canUseFastPath = !options.NoReadAhead && _dialect.IsAscii;
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
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Sets or resets the data of the parser.<br/>
    /// This method should be called after initialization, and after reading a new sequence from an async data source.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetData(in ReadOnlySequence<T> sequence)
    {
        _metaCount = 0;
        _metaIndex = 0;
        _metaMemory = default; // don't hold on to the memory from last read
        _sequence = sequence;
    }

    /// <summary>
    /// Returns a buffer to unescape fields into.
    /// </summary>
    /// <param name="length">Minimum length of the returned span</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal Span<T> GetUnescapeBuffer(int length)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return Options._memoryPool.EnsureCapacity(ref _unescapeBuffer, length, copyOnResize: false).Span;
    }

    /// <summary>
    /// Attempts to return a complete CSV record from the read-ahead buffer.
    /// </summary>
    /// <param name="line">Line containing the record's fields up to the newline</param>
    /// <returns>
    /// <see langword="true"/> if a record was read,
    /// <see langword="false"/> if the buffer is empty or the record is incomplete.
    /// </returns>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetBuffered(out CsvLine<T> line)
    {
        if (_metaIndex < _metaCount)
        {
            ref Meta metaRef = ref MemoryMarshal.GetArrayDataReference(_metaArray);

            if (Meta.TryFindNextEOL(
                    first: ref Unsafe.Add(ref metaRef, 1 + _metaIndex),
                    end: _metaCount - _metaIndex + 1,
                    index: out int fieldCount))
            {
                MetaSegment fields = new() { array = _metaArray, count = fieldCount + 1, offset = _metaIndex };
                line = new CsvLine<T>(
                    parser: this,
                    data: _metaMemory,
                    fields: Unsafe.As<MetaSegment, ArraySegment<Meta>>(ref fields));

                _metaIndex += fieldCount;
                return true;
            }
        }

        Unsafe.SkipInit(out line);
        return false;
    }

    /// <summary>
    /// Attempts to read a complete CSV record from the read-ahead buffer,
    /// or from the underlying data supplied in <see cref="SetData"/>.
    /// </summary>
    /// <param name="line">CSV record</param>
    /// <param name="isFinalBlock">
    /// Determines whether any more data can be expected after this read.
    /// When <see langword="true"/>, the parser will return leftover data even without a trailing newline.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a record was read,
    /// <see langword="false"/> if no record can be read from the underlying data or the read-ahead buffer.
    /// </returns>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadLine(out CsvLine<T> line, bool isFinalBlock)
    {
        if (_metaIndex < _metaCount)
        {
            ref Meta metaRef = ref MemoryMarshal.GetArrayDataReference(_metaArray);

            if (Meta.TryFindNextEOL(
                    first: ref Unsafe.Add(ref metaRef, 1 + _metaIndex),
                    end: _metaCount - _metaIndex + 1,
                    index: out int fieldCount))
            {
                MetaSegment fields = new() { array = _metaArray, count = fieldCount + 1, offset = _metaIndex };
                line = new CsvLine<T>(
                    parser: this,
                    data: _metaMemory,
                    fields: Unsafe.As<MetaSegment, ArraySegment<Meta>>(ref fields));

                _metaIndex += fieldCount;
                return true;
            }
        }

        return TryReadUnbuffered(out line, isFinalBlock);
    }

    /// <summary>
    /// Attempts to read a complete CSV record from the underlying data supplied in <see cref="SetData"/>.
    /// If newline is empty, first call to this method will auto-detect the newline.
    /// </summary>
    /// <param name="line">CSV record</param>
    /// <param name="isFinalBlock">
    /// Determines whether any more data can be expected after this read.
    /// When <see langword="true"/>, the parser will return leftover data even without a trailing newline.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a record was read,
    /// <see langword="false"/> if no record can be read from the underlying data.
    /// </returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool TryReadUnbuffered(out CsvLine<T> line, bool isFinalBlock)
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
                _metaArray = GetMetaBuffer();
                _metaArray[0] = Meta.StartOfData; // the first meta should be one delimiter "behind"
            }

            int fieldCount = ReadFromFirstSpan();

            // see if we read at least one fully formed line
            if (fieldCount != 0 && Meta.HasEOL(MetaBuffer[..fieldCount], out int lastIndex))
            {
                _metaIndex = 0;
                _metaCount = lastIndex + 1;
                _metaMemory = _sequence.First; // cache to avoid calling GetFirstBuffer on every record
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AdvanceAndResetMeta()
    {
        Debug.Assert(_canUseFastPath && _metaIndex != 0);
        Debug.Assert(_newline.Length != 0);
        Debug.Assert(_metaCount >= _metaIndex);

        _metaMemory = default; // don't hold on to the memory from last read

        var lastEOL = _metaArray[_metaIndex];

        if (!lastEOL.IsEOL)
        {
            InvalidState.Throw(GetType(), _metaArray, _metaIndex, _metaCount);
        }

        _sequence = _sequence.Slice(lastEOL.GetNextStart(_newline.Length));
        _metaCount = 0;
        _metaIndex = 0;
    }

    /// <summary>
    /// Advances the reader by how much data was read from the sequence passed with <see cref="SetData"/>.
    /// </summary>
    /// <param name="reader">Reader instance</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AdvanceReader(ICsvPipeReader<T> reader)
    {
        if (_metaIndex != 0)
        {
            AdvanceAndResetMeta();
        }

        reader.AdvanceTo(consumed: _sequence.Start, examined: _sequence.End);
    }

    /// <summary>
    /// Maximum amount of data to read before throwing when auto-detecting newline.
    /// </summary>
    public const int MaxNewlineDetectionLength = 1024;

    /// <summary>
    /// Attempt to auto-detect newline from the data.
    /// </summary>
    /// <returns>True if the current sequence contained a CRLF or LF (checked in that order)</returns>
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

    private protected ArraySegment<Meta> GetSegmentMeta(scoped ReadOnlySpan<Meta> fields)
    {
        Debug.Assert(fields.Length != 0);
        Debug.Assert(_metaIndex == _metaCount);

        if (_metaArray.Length == 0)
        {
            _metaArray = GetMetaBuffer();
        }

        // over a thousand fields?
        if (_metaArray.Length < fields.Length)
        {
            _metaArray = new Meta[fields.Length];
        }

        _metaArray[0] = Meta.StartOfData;
        fields.CopyTo(_metaArray.AsSpan(start: 1));
        return new ArraySegment<Meta>(_metaArray, 0, fields.Length + 1);
    }

    /// <summary>
    /// Disposes the instance.
    /// </summary>
    /// <param name="disposing">Whether the method was called from <see cref="Dispose()"/></param>
    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
            return;

        IsDisposed = true;

        // the memory owners should have their own finalizers if needed
        if (disposing)
        {
            using (_unescapeBuffer)
            using (_multisegmentBuffer)
            {
                _metaCount = 0;
                _metaIndex = 0;

                // don't hold on to any references to the data after disposing
                _sequence = default;
                _metaMemory = default;

                ReturnMetaBuffer(ref _metaArray);
                _metaArray = [];
            }

            _unescapeBuffer = null;
            _multisegmentBuffer = null;
        }
    }
}

// ReSharper disable NotAccessedField.Local
file struct MetaSegment
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
        array[1] = Meta.StartOfData;
        var segment = new MetaSegment { array = array, offset = 1, count = 2 };
        var cast = Unsafe.As<MetaSegment, ArraySegment<Meta>>(ref segment);
        Debug.Assert(cast.Array == array);
        Debug.Assert(cast.Offset == 1);
        Debug.Assert(cast.Count == 2);
    }
#endif
}
// ReSharper restore NotAccessedField.Local

[ExcludeFromCodeCoverage]
file static class InvalidState
{
    public static void Throw(Type parserType, Meta[] metaArray, int metaIndex, int metaCount)
    {
        var error = new System.Text.StringBuilder()
            .Append(parserType.FullName)
            .Append(" was in an invalid state: ")
            .Append(metaCount)
            .Append(" metas were read and ")
            .Append(metaIndex)
            .Append(" were consumed, but the last consumed was not a newline. Metas: ")
            .AppendJoin(", ", metaArray.Index());

        throw new UnreachableException(error.ToString());
    }
}
