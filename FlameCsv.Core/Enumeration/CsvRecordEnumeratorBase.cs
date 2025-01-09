using System.Diagnostics;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.Enumeration;

/// <summary>
/// An enumerator that parses CSV records.
/// </summary>
/// <remarks>
/// If the options are configured to read a header record, it will be processed first before any records are yielded.<br/>
/// This class is not thread-safe, and should not be used concurrently.<br/>
/// The enumerator should always be disposed after use, either explicitly or using <c>foreach</c>.
/// </remarks>
[MustDisposeResource]
public abstract class CsvRecordEnumeratorBase<T> : IDisposable where T : unmanaged, IBinaryInteger<T>
{
    public CsvValueRecord<T> Current => _current._options is not null ? _current : ThrowInvalidCurrentAccess();

    public int Line { get; protected set; }
    public long Position { get; protected set; }

    [HandlesResourceDisposal]
    private readonly EnumeratorState<T> _state;

    [HandlesResourceDisposal]
    protected readonly CsvParser<T> _parser;
    protected CsvValueRecord<T> _current;
    protected bool _disposed;

    internal CsvRecordEnumeratorBase(CsvOptions<T> options)
    {
        _parser = CsvParser<T>.Create(options);
        _state = new EnumeratorState<T>(_parser);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    protected bool MoveNextCore(bool isFinalBlock)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

    Retry:
        if (_parser.TryReadLine(out ReadOnlyMemory<T> line, out CsvRecordMeta meta, isFinalBlock))
        {
            long oldPosition = Position;

            Position += line.Length + (_parser._newline.Length * (!isFinalBlock).ToByte());
            Line++;

            if (_parser.SkipRecord(line, Line, isHeader: _state.NeedsHeader))
            {
                goto Retry;
            }

            CsvValueRecord<T> record = new(oldPosition, Line, line, _parser._options, meta, _state);

            if (_state.NeedsHeader)
            {
                _state.Header = CreateHeader(in record);
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
            using (_state)
            using (_parser)
            {
                _current = default;
            }
        }
    }

    private CsvHeader CreateHeader(ref readonly CsvValueRecord<T> headerRecord)
    {
        BufferFieldReader<T> reader = headerRecord._state.CreateFieldReader();
        StringScratch scratch = default;
        ValueListBuilder<string> list = new(scratch);

        try
        {
            while (reader.MoveNext())
            {
                list.Append(_parser._options.GetAsString(reader.Current));
            }

            string[] header = list.AsSpan().ToArray();

            for (int i = 0; i < header.Length; i++)
            {
                for (int j = 0; j < header.Length; j++)
                {
                    if (i != j && _parser._options.Comparer.Equals(header[i], header[j]))
                    {
                        ThrowExceptionForDuplicateHeaderField(i, j, header[i], headerRecord);
                    }
                }
            }

            return new CsvHeader(_parser._options.Comparer, list.AsSpan().ToArray());
        }
        finally
        {
            list.Dispose();
            reader.Dispose();
        }
    }

    private CsvValueRecord<T> ThrowInvalidCurrentAccess()
    {
        if (_disposed)
            Throw.ObjectDisposed_Enumeration();

        throw new InvalidOperationException("Current was accessed before the enumeration started.");
    }

    private void ThrowExceptionForDuplicateHeaderField(
        int index1,
        int index2,
        string field,
        CsvValueRecord<T> record)
    {
        throw new CsvFormatException(
            $"Duplicate header field \"{field}\" in fields {index1} and {index2} in CSV: "
            + _parser._options.AsPrintableString(record.RawRecord.Span));
    }
}
