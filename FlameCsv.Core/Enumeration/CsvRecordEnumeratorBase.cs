using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
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
public abstract class CsvRecordEnumeratorBase<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Logical 1-based line where the current record is in the CSV.
    /// </summary>
    public int Line { get; protected set; }

    /// <summary>
    /// Absolute position in the data source where current record starts.
    /// </summary>
    public long Position { get; protected set; }

    /// <summary>
    /// Whether the enumerator has been disposed.
    /// </summary>
    protected bool IsDisposed => _version == -1;

    private int _version;

    [HandlesResourceDisposal] private protected readonly CsvParser<T> _parser;
    private protected CsvValueRecord<T> _current;

    internal readonly bool _hasHeader;
    private readonly bool _validateFieldCount;
    private readonly CsvRecordCallback<T>? _callback;

    private Dictionary<object, object>? _materializerCache;
    private int? _expectedFieldCount;
    private CsvHeader? _header;

    internal Dictionary<object, object> MaterializerCache
        => _materializerCache ??= new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Current header value. May be null if a header is not yet read, header is reset, or if the CSV has no header.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="NotSupportedException">Thrown when the CSV has no header and a non-null value is set.</exception>
    public CsvHeader? Header
    {
        get => _header;
        set
        {
            Throw.IfEnumerationDisposed(_version == -1);

            if (!_hasHeader && value is not null)
                Throw.NotSupported_CsvHasNoHeader();

            if (EqualityComparer<CsvHeader>.Default.Equals(Header, value))
                return;

            if (Header is not null && value is not null)
                Throw.Unreachable_AlreadyHasHeader();

            _header = value;
            _expectedFieldCount = value?.Count;
            _materializerCache?.Clear();
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvRecordEnumeratorBase{T}"/> class.
    /// </summary>
    protected CsvRecordEnumeratorBase(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _parser = CsvParser.Create(options);
        _hasHeader = options._hasHeader;
        _validateFieldCount = options._validateFieldCount;
        _callback = options._recordCallback;

        // clear the materializer cache on hot reload
        HotReloadService.RegisterForHotReload(
            this,
            static state => ((CsvRecordEnumeratorBase<T>)state)._materializerCache = null);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected bool MoveNextCore(bool isFinalBlock)
    {
        Throw.IfEnumerationDisposed(_version == -1);

    Retry:
        if (!_parser.TryReadLine(out CsvLine<T> line, isFinalBlock))
        {
            return false;
        }

        long recordPosition = Position;

        Position += line.RecordLength + (_parser._newline.Length * (!isFinalBlock).ToByte());
        Line++;

        if (_callback is not null)
        {
            bool skip = false;
            bool headerRead = Header is not null;

            CsvRecordCallbackArgs<T> args = new(
                in line,
                Header is { } header ? header.Values : [],
                Line,
                recordPosition,
                ref skip,
                ref headerRead);
            _callback.Invoke(in args);

            if (!headerRead) Header = null;
            if (skip) goto Retry;
        }

        // header needs to be read
        if (_hasHeader && _header is null)
        {
            Header = CreateHeader(in line);
            goto Retry;
        }

        _version++;

        if (_validateFieldCount)
        {
            if (_expectedFieldCount is null)
            {
                _expectedFieldCount = line.FieldCount;
            }
            else if (line.FieldCount != _expectedFieldCount.Value)
            {
                Throw.InvalidData_FieldCount(_expectedFieldCount.Value, line.FieldCount);
            }
        }

        _current = new CsvValueRecord<T>(_version, recordPosition, Line, in line, _parser.Options, this);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnsureVersion(int version)
    {
        if (_version == -1)
            Throw.ObjectDisposed_Enumeration();

        if (version != _version)
            Throw.InvalidOp_EnumerationChanged();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(Header))]
    internal bool TryGetHeaderIndex(string name, out int index)
    {
        ArgumentNullException.ThrowIfNull(name);
        Throw.IfEnumerationDisposed(_version == -1);

        if (!_hasHeader)
            Throw.NotSupported_CsvHasNoHeader();

        if (Header is null)
            Throw.InvalidOperation_HeaderNotRead();

        return Header.TryGetValue(name, out index);
    }

    /// <summary>
    /// Disposes the underlying data source and internal states, and returns pooled memory.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the underlying data source and internal states, and returns pooled memory.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_version == -1)
            return;

        _version = -1;

        if (disposing)
        {
            using (_parser)
            {
                _version = -1;
                _current = default;
                _materializerCache = null;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private CsvHeader CreateHeader(ref readonly CsvLine<T> headerRecord)
    {
        MetaFieldReader<T> reader = new(in headerRecord, stackalloc T[Token<T>.StackLength]);
        return CsvHeader.Parse(_parser.Options, ref reader);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private protected void ThrowInvalidCurrentAccess()
    {
        if (_version == -1)
            Throw.ObjectDisposed_Enumeration();

        throw new InvalidOperationException("Current was accessed before the enumeration started.");
    }
}

