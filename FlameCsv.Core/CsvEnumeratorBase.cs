using System.Buffers;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Configuration;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

/// <summary>
/// An enumerator that parses CSV records.
/// </summary>
/// <remarks>
/// If the options are configured to read a header record, it will be processed first before any records are yielded.<br/>
/// This class is not thread-safe, and should not be used concurrently.<br/>
/// The enumerator should always be disposed after use, either explicitly or using <c>foreach</c>.
/// </remarks>
public abstract class CsvEnumeratorBase<T> : IDisposable
    where T : unmanaged, IEquatable<T>
{
    public CsvDialect<T> Dialect => _dialect;
    public CsvRecord<T> Current { get; protected set; }
    public int Line { get; protected set; }
    public long Position { get; protected set; }

    protected readonly CsvReaderOptions<T> _options;
    protected readonly CancellationToken _cancellationToken;

    private readonly CsvDialect<T> _dialect;
    private readonly CsvEnumerationState<T> _state;
    private readonly ArrayPool<T> _arrayPool;
    private T[]? _multisegmentBuffer;

    protected CsvEnumeratorBase(CsvReaderOptions<T> options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _cancellationToken = cancellationToken;
        _dialect = new CsvDialect<T>(options);
        _state = new CsvEnumerationState<T>(options);
        _arrayPool = options.ArrayPool.AllocatingIfNull();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected internal bool MoveNextCore(ref ReadOnlySequence<T> data, bool isFinalBlock)
    {
        Retry:
        if (RFC4180Mode<T>.TryGetLine(in _dialect, ref data, out ReadOnlySequence<T> line, out int quoteCount, isFinalBlock))
        {
            MoveNextOrReadHeader(in line, quoteCount, out bool headerRead);

            if (!isFinalBlock)
            {
                Position += _dialect.Newline.Length; // increment position _after_ record has been initialized
            }

            if (!headerRead)
            {
                return true;
            }

            goto Retry;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void MoveNextOrReadHeader(in ReadOnlySequence<T> line, int quoteCount, out bool headerRead)
    {
        ReadOnlyMemory<T> memory;

        if (line.IsSingleSegment)
        {
            memory = line.First;
        }
        else
        {
            int length = (int)line.Length;
            _arrayPool.EnsureCapacity(ref _multisegmentBuffer, length);
            line.CopyTo(_multisegmentBuffer);
            memory = _multisegmentBuffer.AsMemory(0, length);
        }

        CsvRecord<T> record = new(Position, ++Line, memory, _options, quoteCount, _state);
        Position += memory.Length;

        if (!_state.NeedsHeader)
        {
            Current = record;
            headerRead = false;
        }
        else
        {
            _state.SetHeader(CreateHeaderDictionary(record));
            headerRead = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _arrayPool.EnsureReturned(ref _multisegmentBuffer);
            _state.Dispose();
        }
    }

    private Dictionary<string, int> CreateHeaderDictionary(CsvRecord<T> headerRecord)
    {
        ICsvStringConfiguration<T> config = (ICsvStringConfiguration<T>)_options;

        Dictionary<string, int> dictionary = new(
            capacity: headerRecord.GetFieldCount(),
            comparer: StringComparer.FromComparison(config.Comparison));

        int index = 0;

        foreach (var field in headerRecord)
        {
            string fieldString = config.GetTokensAsString(field.Span);

            if (!dictionary.TryAdd(fieldString, index++))
            {
                ThrowExceptionForDuplicateHeaderField(fieldString, headerRecord);
            }
        }

        return dictionary;
    }

    private void ThrowExceptionForDuplicateHeaderField(string field, CsvRecord<T> record)
    {
        if (_options.AllowContentInExceptions)
        {
            throw new CsvFormatException(
                $"Duplicate header field \"{field}\" in CSV: {record.Data.Span.AsPrintableString(true, record.Dialect)}");
        }
        else
        {
            throw new CsvFormatException("Duplicate header field in CSV.");
        }
    }
}
