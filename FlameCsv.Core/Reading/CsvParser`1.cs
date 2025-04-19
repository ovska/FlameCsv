using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.IO;
using FlameCsv.Reading.Internal;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Reads CSV records from a <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <remarks>
/// Internal implementation detail, this type should probably not be used directly.
/// </remarks>
[MustDisposeResource]
public abstract partial class CsvParser<T> : CsvParser, IDisposable, IAsyncDisposable
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Current options instance.
    /// </summary>
    public CsvOptions<T> Options { get; }

    internal readonly CsvDialect<T> _dialect;

    private protected readonly Allocator<T> _multisegmentAllocator;
    internal readonly Allocator<T> _unescapeAllocator;

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
    /// Index of the last EOL read from <see cref="ParseFromBuffer"/>.
    /// </summary>
    /// <remarks>
    /// Does not include the start-of-data meta, e.g. this points to <see cref="MetaBuffer"/>, not the underlying array.
    /// </remarks>
    /// <seealso cref="_metaArray"/>
    private int _metaCount;

    /// <summary>
    /// How many read ahead fields have been consumed.
    /// </summary>
    /// <remarks>
    /// Does not include the start-of-data meta, e.g. this points to <see cref="MetaBuffer"/>, not the underlying array.
    /// </remarks>
    private int _metaIndex;

    private readonly ICsvBufferReader<T> _reader;
    private ReadOnlyMemory<T> _buffer;
    private int _bufferConsumed;

    /// <summary>
    /// Current buffer available for the parser.
    /// </summary>
    protected ReadOnlySpan<T> Buffer => _buffer.Span;

    /// <summary>
    /// Whether any more data can be read.
    /// </summary>
    protected bool IsReaderCompleted => _readerCompleted;

    /// <summary>
    /// Whether the reader has completed, and no more data can be read.
    /// </summary>
    private bool _readerCompleted;

    /// <summary>
    /// Whether the UTF-8 BOM should be skipped on the next (first) read.
    /// </summary>
    private bool _skipBOM;

    /// <summary>
    /// Whether <see cref="NewlineBuffer{T}.Second"/> needs to be trimmed from the start of the next sequence.
    /// </summary>
    private protected bool _previousEndCR;

    private protected CsvParser(CsvOptions<T> options, ICsvBufferReader<T> reader, in CsvParserOptions<T> parserOptions)
    {
        Debug.Assert(options.IsReadOnly);

        Options = options;
        _dialect = options.Dialect;
        _metaArray = [];
        _reader = reader;
        _skipBOM = typeof(T) == typeof(byte);

        _multisegmentAllocator = parserOptions.MultiSegmentAllocator ?? new MemoryPoolAllocator<T>(options.Allocator);
        _unescapeAllocator = parserOptions.UnescapeAllocator ?? new MemoryPoolAllocator<T>(options.Allocator);
    }

    /// <summary>
    /// Parses fields from <see cref="Buffer"/> into <see cref="MetaBuffer"/>.<br/>
    /// This method should <see cref="IsReaderCompleted"/> into account.
    /// </summary>
    /// <returns>Number of fields parsed</returns>
    /// <seealso cref="_metaArray"/>
    private protected abstract int ParseFromBuffer();

    /// <summary>
    /// Attempts to return a complete CSV record from the read-ahead buffer.
    /// </summary>
    /// <param name="fields">Fields of the CSV record up to the newline</param>
    /// <returns>
    /// <see langword="true"/> if a record was read,
    /// <see langword="false"/> if the read-ahead buffer is empty or the record is incomplete.
    /// </returns>
    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool TryGetBuffered(out CsvFields<T> fields)
    {
        if (_metaIndex < _metaCount)
        {
            ref Meta metaRef = ref MemoryMarshal.GetArrayDataReference(_metaArray);

            if (Meta.TryFindNextEOL(
                    first: ref Unsafe.Add(ref metaRef, 1 + _metaIndex),
                    end: _metaCount - _metaIndex + 1,
                    index: out int fieldCount))
            {
                MetaSegment fieldMeta = new() { array = _metaArray, count = fieldCount + 1, offset = _metaIndex };
                fields = new CsvFields<T>(
                    parser: this,
                    data: _buffer,
                    fieldMeta: Unsafe.As<MetaSegment, ArraySegment<Meta>>(ref fieldMeta));

                _metaIndex += fieldCount;
                return true;
            }
        }

        Unsafe.SkipInit(out fields);
        return false;
    }

    /// <summary>
    /// Attempts to read the next CSV record from the buffered data.
    /// </summary>
    /// <param name="fields"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryReadLine(out CsvFields<T> fields)
    {
        return TryGetBuffered(out fields) || TryFillBuffer(out fields);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryFillBuffer(out CsvFields<T> fields)
    {
        if (_buffer.IsEmpty)
        {
            goto End;
        }

        // this should be called only when the previous metas have been exhausted
        if (_metaIndex != 0)
        {
            ResetMetaBuffer();
        }

        // delay the rent until first read
        if (_metaArray.Length == 0)
        {
            _metaArray = GetMetaBuffer();
            _metaArray[0] = Meta.StartOfData; // the first meta should be one delimiter "behind"
        }

        int fieldCount = ParseFromBuffer();

        if (fieldCount == 0)
        {
            goto End;
        }

        // see if we read at least one fully formed line
        if (Meta.HasEOL(MetaBuffer[..fieldCount], out int lastIndex))
        {
            _metaIndex = 0;
            _metaCount = lastIndex + 1;
            _bufferConsumed = 0;
            bool retVal = TryGetBuffered(out fields);
            Debug.Assert(retVal, "Should have been able to get the buffered fields");
            return true;
        }

        // we have some data but no trailing newline. consume the final record
        if (_readerCompleted)
        {
            fields = new(this, _buffer, new ArraySegment<Meta>(_metaArray, 0, fieldCount + 1));
            _buffer = ReadOnlyMemory<T>.Empty;
            return true;
        }

    End:
        Unsafe.SkipInit(out fields);
        return false;
    }

    /// <summary>
    /// Attempts to reset the parser to the beginning of the data source.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the inner data source supports resetion and was successfully reset;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool TryReset()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (_reader.TryReset())
        {
            SetReadResult(in CsvReadResult<T>.Empty);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempt to read more data from the underlying data.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if more data was read; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// All further reads after returning <see langword="false"/> will also return <see langword="false"/>.
    /// </remarks>
    public bool TryAdvanceReader()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (!_readerCompleted)
        {
            AdvanceReader();
            CsvReadResult<T> result = _reader.Read();
            SetReadResult(in result);
            return true;
        }

        return false;
    }

    /// <inheritdoc cref="TryAdvanceReader"/>
    // TODO: profile pooling task builder
    public async ValueTask<bool> TryAdvanceReaderAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_readerCompleted)
        {
            AdvanceReader();
            CsvReadResult<T> result = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            SetReadResult(in result);
            return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ResetMetaBuffer()
    {
        Debug.Assert(_metaIndex != 0);
        Debug.Assert(_metaCount >= _metaIndex);

        var lastEOL = _metaArray[_metaIndex];

        if (!lastEOL.IsEOL)
        {
            InvalidState.Throw(GetType(), _metaArray, _metaIndex, _metaCount);
        }

        _previousEndCR = lastEOL.EndsInCR(_buffer.Span, in _dialect._newline);
        _buffer = _buffer.Slice(lastEOL.NextStart);
        _metaCount = 0;
        _metaIndex = 0;
    }

    /// <summary>
    /// Advances the reader.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AdvanceReader()
    {
        if (_metaIndex != 0)
        {
            ResetMetaBuffer();
        }

        _reader.Advance(_bufferConsumed);
        _bufferConsumed = 0;
    }

    /// <summary>
    /// Sets the result of a read operation.
    /// </summary>
    /// <param name="result">Result of the previous read to <see cref="ICsvBufferReader{T}"/></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetReadResult(ref readonly CsvReadResult<T> result)
    {
        _metaCount = 0;
        _metaIndex = 0;
        _buffer = result.Buffer;
        _readerCompleted = result.IsCompleted;

        if (typeof(T) == typeof(byte) && _skipBOM) TrySkipBOM();
        if (_previousEndCR) TrySkipLF();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TrySkipBOM()
    {
        Debug.Assert(typeof(T) == typeof(byte));

        if (_buffer.Span is [(byte)0xEF, (byte)0xBB, (byte)0xBF, ..])
        {
            _buffer = _buffer.Slice(3);
        }

        _skipBOM = false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void TrySkipLF()
    {
        Debug.Assert(_previousEndCR);
        Debug.Assert(_dialect.Newline.Length == 2);

        if (_buffer.Span is [var first, ..] && first == _dialect.Newline.Second)
        {
            _buffer = _buffer.Slice(1);
        }

        _previousEndCR = false;
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
        if (_metaArray.Length < (fields.Length + 1))
        {
            _metaArray = new Meta[fields.Length + 1];
        }

        _metaArray[0] = Meta.StartOfData;
        fields.CopyTo(MetaBuffer);
        return new ArraySegment<Meta>(_metaArray, 0, fields.Length + 1);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed) return;
        GC.SuppressFinalize(this);

        using (_reader)
        {
            Dispose(disposing: true);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (IsDisposed) return;
        GC.SuppressFinalize(this);

        await using (_reader.ConfigureAwait(false))
        {
            Dispose(true);
        }
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
            using (_unescapeAllocator)
            using (_multisegmentAllocator)
            {
                _metaCount = 0;
                _metaIndex = 0;

                // don't hold on to any references to the data after disposing
                _buffer = ReadOnlyMemory<T>.Empty;

                ReturnMetaBuffer(ref _metaArray);
                _metaArray = [];
            }
        }
    }
}

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
