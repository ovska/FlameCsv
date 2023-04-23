using System.Buffers;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
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
public abstract class CsvEnumeratorBase<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public CsvDialect<T> Dialect => _dialect;
    public CsvValueRecord<T> Current { get; protected set; }
    public int Line { get; protected set; }
    public long Position { get; protected set; }

    protected readonly CsvReaderOptions<T> _options;

    private readonly CsvDialect<T> _dialect;
    private readonly CsvEnumerationState<T> _state;
    private readonly ArrayPool<T> _arrayPool;
    private readonly CsvCallback<T, bool>? _shouldSkipRow;
    private T[]? _multisegmentBuffer;

    protected bool _disposed;

    protected CsvEnumeratorBase(CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.MakeReadOnly();

        _options = options;
        _dialect = new CsvDialect<T>(options);
        _state = new CsvEnumerationState<T>(options);
        _arrayPool = options.ArrayPool.AllocatingIfNull();
        _shouldSkipRow = options.ShouldSkipRow;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected bool MoveNextCore(ref ReadOnlySequence<T> data, bool isFinalBlock)
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(GetType().Name);

        Retry:
        if (_dialect.TryGetLine(ref data, out ReadOnlySequence<T> line, out RecordMeta meta, isFinalBlock))
        {
            ReadOnlyMemory<T> memory = line.AsMemory(ref _multisegmentBuffer, _arrayPool);

            long oldPosition = Position;

            Position += memory.Length + _dialect.Newline.Length * (!isFinalBlock).ToByte();
            Line++;

            if (_shouldSkipRow?.Invoke(memory, in _dialect) ?? false)
            {
                goto Retry;
            }

            CsvValueRecord<T> record = new(oldPosition, Line, memory, _options, meta, _state);

            if (_state.NeedsHeader)
            {
                _state.SetHeader(CreateHeaderDictionary(record));
                goto Retry;
            }

            Current = record;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

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

    private Dictionary<string, int> CreateHeaderDictionary(CsvValueRecord<T> headerRecord)
    {
        Dictionary<string, int> dictionary = new(
            capacity: headerRecord.GetFieldCount(),
            comparer: StringComparer.FromComparison(_options.Comparison));

        int index = 0;

        foreach (ReadOnlyMemory<T> field in headerRecord)
        {
            string fieldString = _options.GetAsString(field.Span);

            if (!dictionary.TryAdd(fieldString, index++))
            {
                ThrowExceptionForDuplicateHeaderField(fieldString, headerRecord);
            }
        }

        return dictionary;
    }

    private void ThrowExceptionForDuplicateHeaderField(string field, CsvValueRecord<T> record)
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
