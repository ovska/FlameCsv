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
public sealed partial class CsvRecordEnumerator<T>
    : CsvEnumeratorBase<T>,
        IEnumerator<CsvRecord<T>>,
        IAsyncEnumerator<CsvRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Gets the current record.
    /// </summary>
    /// <remarks>
    /// The value should not be held onto after the enumeration continues or ends, as the records wrap
    /// shared and/or pooled memory.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">Thrown when the enumerator has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown when enumeration has not yet started.</exception>
    public ref readonly CsvRecord<T> Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (_current._owner is null)
            {
                ThrowInvalidCurrentAccess();
            }

            return ref _current;
        }
    }

    CsvRecord<T> IEnumerator<CsvRecord<T>>.Current => Current;
    CsvRecord<T> IAsyncEnumerator<CsvRecord<T>>.Current => Current;
    object IEnumerator.Current => Current;

    void IEnumerator.Reset()
    {
        ResetCore();
        _version = 0;
        _current = default;
        _materializerCache?.Clear();
        _header = null;
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
        CancellationToken cancellationToken = default
    )
        : base(options, reader, cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(reader);

        _hasHeader = options.HasHeader;

        // clear the materializer cache on hot reload
        HotReloadService.RegisterForHotReload(
            this,
            static state => ((CsvRecordEnumerator<T>)state)._materializerCache = null
        );
    }

    private int _version;

    private CsvRecord<T> _current;
    internal readonly bool _hasHeader;
    private Dictionary<object, object>? _materializerCache;
    private CsvHeader? _header;

    internal Dictionary<object, object> MaterializerCache =>
        _materializerCache ??= new(ReferenceEqualityComparer.Instance);

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
            if (_version is -1)
                Throw.ObjectDisposed_Enumeration(this);

            if (!_hasHeader && value is not null)
                Throw.NotSupported_CsvHasNoHeader();

            if (EqualityComparer<CsvHeader>.Default.Equals(Header, value))
                return;

            _header = value;
            _materializerCache?.Clear();
        }
    }

    /// <inheritdoc/>
    protected override ImmutableArray<string> GetHeader() => Header?.Values ?? default;

    /// <inheritdoc/>
    protected override void ResetHeader() => Header = null;

    /// <inheritdoc/>
    internal override bool MoveNextCore(RecordView view)
    {
        if (_version is -1)
            Throw.ObjectDisposed_Enumeration(this);

        // header needs to be read
        if (_hasHeader && _header is null)
        {
            CsvRecordRef<T> record = new(_reader, view);
            Header = new CsvHeader(Options.IgnoreHeaderCase, CsvHeader.Parse(record));
            return false;
        }

        _current = new CsvRecord<T>(++_version, view, this);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void EnsureVersion(int version)
    {
        if (_version == version)
        {
            return;
        }

        ThrowInvalidVersion(version);
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

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidCurrentAccess()
    {
        if (_version == -1)
            Throw.ObjectDisposed_Enumeration(this);

        throw new InvalidOperationException("Current was accessed before the enumeration started.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowInvalidVersion(int version)
    {
        if (_version == -1)
            Throw.ObjectDisposed_Enumeration(this);

        if (version != _version)
            Throw.InvalidOp_EnumerationChanged();
    }
}
