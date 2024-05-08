using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

/// <summary>
/// An enumerator that parses CSV records.
/// </summary>
/// <remarks>
/// If the options are configured to read a header record, it will be processed first before any records are yielded.<br/>
/// This class is not thread-safe, and should not be used concurrently.<br/>
/// The enumerator should always be disposed after use, either explicitly or using <c>foreach</c>.
/// </remarks>
public abstract class CsvRecordEnumeratorBase<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public CsvValueRecord<T> Current => _current._options is not null ? _current : ThrowInvalidCurrentAccess();

    public int Line { get; protected set; }
    public long Position { get; protected set; }

    private readonly CsvReadingContext<T> _context;
    private readonly EnumeratorState<T> _state;

    protected internal readonly CsvDataReader<T> _data;
    protected internal CsvValueRecord<T> _current;
    protected internal bool _disposed;

    internal CsvRecordEnumeratorBase(in CsvReadingContext<T> context)
    {
        context.EnsureValid();
        _context = context;
        _state = new EnumeratorState<T>(in _context);
        _data = new();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected bool MoveNextCore(bool isFinalBlock)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Retry:
        if (_context.TryReadLine(_data, out ReadOnlyMemory<T> line, out RecordMeta meta, isFinalBlock))
        {
            long oldPosition = Position;

            Position += line.Length + _context.Dialect.Newline.Length * (!isFinalBlock).ToByte();
            Line++;

            if (_context.SkipRecord(line, Line, _state.Header is not null))
            {
                goto Retry;
            }

            CsvValueRecord<T> record = new(oldPosition, Line, line, _context.Options, meta, _state);

            if (_state.NeedsHeader)
            {
                _state.SetHeader(CreateHeaderDictionary(record));
                goto Retry;
            }

            _current = record;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;

        if (disposing)
        {
            _current = default;
            _data.Reader = default;
            _context.ArrayPool.EnsureReturned(ref _data.MultisegmentBuffer);
            _state.Dispose();
        }
    }

    private Dictionary<string, int> CreateHeaderDictionary(CsvValueRecord<T> headerRecord)
    {
        Dictionary<string, int> dictionary = new(capacity: headerRecord.GetFieldCount(), comparer: _context.Options.Comparer);

        int index = 0;

        foreach (ReadOnlyMemory<T> field in headerRecord)
        {
            string fieldString = _context.Options.GetAsString(field.Span);

            if (!dictionary.TryAdd(fieldString, index++))
            {
                ThrowExceptionForDuplicateHeaderField(fieldString, headerRecord);
            }
        }

        return dictionary;
    }

    private CsvValueRecord<T> ThrowInvalidCurrentAccess()
    {
        if (_disposed)
            Throw.ObjectDisposed_Enumeration();

        throw new InvalidOperationException("Current was accessed before the enumeration started.");
    }

    private void ThrowExceptionForDuplicateHeaderField(string field, CsvValueRecord<T> record)
    {
        throw new CsvFormatException(
            $"Duplicate header field \"{field}\" in CSV: {_context.AsPrintableString(record.RawRecord)}");
    }
}
