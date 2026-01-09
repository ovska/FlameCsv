using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Callback called for every record (line) before it is processed by the library.
/// </summary>
[PublicAPI]
public delegate void CsvRecordCallback<T>(ref readonly CsvRecordCallbackArgs<T> args)
    where T : unmanaged, IBinaryInteger<T>;

/// <summary>
/// Arguments for <see cref="CsvRecordCallback{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public readonly ref struct CsvRecordCallbackArgs<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvRecordRef<T> _record;
    private readonly ref bool _skip;
    private readonly ref bool _headerRead;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvRecordCallbackArgs{T}"/>.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public CsvRecordCallbackArgs(
        CsvRecordRef<T> record,
        ImmutableArray<string> header,
        ref bool skip,
        ref bool headerRead
    )
        : this(record, header.AsSpan(), ref skip, ref headerRead)
    {
        if (record._owner is null)
            Throw.Argument_DefaultStruct(typeof(CsvRecordRef<T>), nameof(record));

        if (header.IsDefault)
            Throw.ArgumentNull(nameof(header));

        if (Unsafe.IsNullRef(ref skip))
            Throw.ArgumentNull(nameof(skip));

        if (Unsafe.IsNullRef(ref headerRead))
            Throw.ArgumentNull(nameof(headerRead));
    }

    internal CsvRecordCallbackArgs(
        CsvRecordRef<T> record,
        ReadOnlySpan<string> header,
        ref bool skip,
        ref bool headerRead
    )
    {
        // use separate private ctor to avoid validation overhead since we know the args are valid
        _record = record;
        Header = header;
        _skip = ref skip;
        _headerRead = ref headerRead;
    }

    /// <summary>
    /// Returns true if the record has a length of 0.
    /// </summary>
    /// <remarks>
    /// An empty record's field count is always 1 (it contains a single empty field).
    /// </remarks>
    public bool IsEmpty => _record.GetRecordLength() == 0;

    /// <summary>
    /// The current CSV record.
    /// </summary>
    [UnscopedRef]
    public ref readonly CsvRecordRef<T> Record
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _record;
    }

    /// <summary>
    /// Options instance.
    /// </summary>
    public CsvOptions<T> Options => _record._owner.Options;

    /// <inheritdoc cref="CsvRecordRef{T}.LineNumber"/>
    public int LineNumber => _record.LineNumber;

    /// <inheritdoc cref="CsvRecordRef{T}.Position"/>
    public long Position => _record.Position;

    /// <summary>
    /// Set to true to skip this record.
    /// </summary>
    public bool SkipRecord
    {
        get => _skip;
        set => _skip = value;
    }

    /// <summary>
    /// Whether the header has been read.
    /// If this is set to false when the callback returns, the header is re-read.
    /// </summary>
    /// <remarks>
    /// If the set to false while reading headered CSV, this record will be considered the header unless
    /// <see cref="SkipRecord"/> is also true, in which case it will be first unskipped record.<br/>
    /// Modifying the value does nothing in parallel workloads, as the order of records is not guaranteed.
    /// </remarks>
    public bool HeaderRead
    {
        get => _headerRead;
        set => _headerRead = value;
    }

    /// <summary>
    /// Returns the header record if <see cref="HeaderRead"/> is true, empty otherwise.
    /// </summary>
    public ReadOnlySpan<string> Header { get; }
}
