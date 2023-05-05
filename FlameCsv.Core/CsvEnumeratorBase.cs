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
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Design",
    "CA1051:Do not declare visible instance fields",
    Justification = "The constructor is internal so this type cannot be inherited outside the library")]
public abstract class CsvEnumeratorBase<T> : IDisposable where T : unmanaged, IEquatable<T>
{
    public CsvValueRecord<T> Current => _current._options is not null ? _current : ThrowInvalidCurrentAccess();

    public int Line { get; protected set; }
    public long Position { get; protected set; }

    private readonly CsvReadingContext<T> _context;
    private readonly CsvEnumerationState<T> _state;

    private T[]? _multisegmentBuffer; // rented array for multi-segmented lines

    protected CsvValueRecord<T> _current;

    protected internal bool _disposed;

    internal CsvEnumeratorBase(in CsvReadingContext<T> context)
    {
        context.EnsureValid();
        _context = context;
        _state = new CsvEnumerationState<T>(in _context);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected bool MoveNextCore(ref ReadOnlySequence<T> data, bool isFinalBlock)
    {
        if (_disposed)
            ThrowHelper.ThrowObjectDisposedException(GetType().Name);

        Retry:
        if (_context.TryGetLine(ref data, out ReadOnlySequence<T> line, out RecordMeta meta, isFinalBlock))
        {
            ReadOnlyMemory<T> memory = line.AsMemory(ref _multisegmentBuffer, _context.ArrayPool);

            long oldPosition = Position;

            Position += memory.Length + _context.Dialect.Newline.Length * (!isFinalBlock).ToByte();
            Line++;

            if (_context.SkipRecord(memory, Line))
            {
                goto Retry;
            }

            CsvValueRecord<T> record = new(oldPosition, Line, memory, _context.Options, meta, _state);

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
            _context.ArrayPool.EnsureReturned(ref _multisegmentBuffer);
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
