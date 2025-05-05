using System.Buffers;
using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.IO;
using FlameCsv.Reading;
using FlameCsv.Reading.Internal;
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
public sealed class CsvRecordEnumerator<T>
    : CsvEnumeratorBase<T>, IEnumerator<CsvValueRecord<T>>, IAsyncEnumerator<CsvValueRecord<T>>, IRecordOwner
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Gets the current record.
    /// </summary>
    /// <remarks>
    /// The value should not be held onto after the enumeration continues or ends, as the records wrap
    /// shared and/or pooled memory.
    /// If you must, convert the record to <see cref="CsvRecord{T}"/>.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the enumerator has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when enumeration has not yet started.</exception>
    public ref readonly CsvValueRecord<T> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_current._options is null)
            {
                ThrowInvalidCurrentAccess();
            }

            return ref _current;
        }
    }

    CsvValueRecord<T> IEnumerator<CsvValueRecord<T>>.Current => _current;
    CsvValueRecord<T> IAsyncEnumerator<CsvValueRecord<T>>.Current => _current;
    object IEnumerator.Current => _current;
    void IEnumerator.Reset() => ResetCore();

    internal CsvRecordEnumerator(ReadOnlyMemory<T> csv, CsvOptions<T> options)
        : this(options, CsvBufferReader.Create(csv))
    {
    }

    internal CsvRecordEnumerator(in ReadOnlySequence<T> csv, CsvOptions<T> options)
        : this(options, CsvBufferReader.Create(in csv))
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CsvRecordEnumerator{T}"/>.
    /// </summary>
    /// <param name="options">Options-instance</param>
    /// <param name="reader">Reader for the CSV data</param>
    /// <param name="cancellationToken">Cancellation token used for asynchronous enumeration</param>
    public CsvRecordEnumerator(
        CsvOptions<T> options,
        ICsvBufferReader<T> reader,
        CancellationToken cancellationToken = default)
        : base(options, reader, cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        _hasHeader = options.HasHeader;
        _validateFieldCount = options.ValidateFieldCount;

        // clear the materializer cache on hot reload
        HotReloadService.RegisterForHotReload(
            this,
            static state => ((CsvRecordEnumerator<T>)state)._materializerCache = null);
    }

    private int _version;

    private CsvValueRecord<T> _current;
    internal readonly bool _hasHeader;
    private readonly bool _validateFieldCount;
    private Dictionary<object, object>? _materializerCache;
    private int? _expectedFieldCount;
    private CsvHeader? _header;

    IDictionary<object, object> IRecordOwner.MaterializerCache
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
            _expectedFieldCount = value?.Values.Length;
            _materializerCache?.Clear();
        }
    }

    /// <inheritdoc/>
    protected override ImmutableArray<string> GetHeader() => Header?.Values ?? default;

    /// <inheritdoc/>
    protected override void ResetHeader() => Header = null;

    /// <inheritdoc/>
    protected override bool MoveNextCore(ref readonly CsvFields<T> fields)
    {
        Throw.IfEnumerationDisposed(_version == -1);

        // header needs to be read
        if (_hasHeader && _header is null)
        {
            CreateHeader(in fields);
            return false;
        }

        _version++;

        if (_validateFieldCount)
        {
            if (_expectedFieldCount is null)
            {
                _expectedFieldCount = fields.FieldCount;
            }
            else if (fields.FieldCount != _expectedFieldCount.Value)
            {
                Throw.InvalidData_FieldCount(_expectedFieldCount.Value, fields.FieldCount);
            }
        }

        _current = new CsvValueRecord<T>(_version, Position, Line, in fields, Reader.Options, this);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IRecordOwner.EnsureVersion(int version)
    {
        if (_version == -1)
            Throw.ObjectDisposed_Enumeration();

        if (version != _version)
            Throw.InvalidOp_EnumerationChanged();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _version = -1;
            _current = default;
            _materializerCache = null;
        }
    }

    /// <inheritdoc/>
    protected override ValueTask DisposeAsyncCore()
    {
        _version = -1;
        _current = default;
        _materializerCache = null;
        return default;
    }

    [MemberNotNull(nameof(Header))]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void CreateHeader(ref readonly CsvFields<T> headerRecord)
    {
        CsvFieldsRef<T> reader = new(in headerRecord);
        ImmutableArray<string> values = CsvHeader.Parse(Options, ref reader);
        Header = new CsvHeader(Options.Comparer, values);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidCurrentAccess()
    {
        if (_version == -1)
            Throw.ObjectDisposed_Enumeration();

        throw new InvalidOperationException("Current was accessed before the enumeration started.");
    }
}
