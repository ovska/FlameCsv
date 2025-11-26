using System.Collections;
using FlameCsv.Extensions;

namespace FlameCsv.Enumeration;

/// <summary>
/// Enumerates data into CSV records.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <remarks>
/// This type is only intended to be used directly within a <c>foreach</c> loop.
/// </remarks>
public readonly struct CsvRecordEnumerable<T> : IEnumerable<CsvRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly Csv.IReadBuilderBase<T> _builder;
    private readonly CsvOptions<T> _options;

    internal CsvRecordEnumerable(Csv.IReadBuilderBase<T> builder, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        _builder = builder;
        _options = options;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public CsvRecordEnumerator<T> GetEnumerator()
    {
        if (_builder is null)
        {
            Throw.InvalidOp_DefaultStruct(GetType());
        }

        return new CsvRecordEnumerator<T>(_options, _builder.CreateReader(isAsync: false));
    }

    IEnumerator<CsvRecord<T>> IEnumerable<CsvRecord<T>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Enumerates data asynchronously into CSV records.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <remarks>
/// This type is only intended to be used directly within a <c>await foreach</c> loop.
/// </remarks>
public readonly struct CsvRecordAsyncEnumerable<T> : IAsyncEnumerable<CsvRecord<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly Csv.IReadBuilderBase<T> _builder;
    private readonly CsvOptions<T> _options;

    internal CsvRecordAsyncEnumerable(Csv.IReadBuilderBase<T> builder, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        _builder = builder;
        _options = options;
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public CsvRecordEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (_builder is null)
        {
            Throw.InvalidOp_DefaultStruct(GetType());
        }

        return new CsvRecordEnumerator<T>(_options, _builder.CreateReader(isAsync: true), cancellationToken);
    }

    IAsyncEnumerator<CsvRecord<T>> IAsyncEnumerable<CsvRecord<T>>.GetAsyncEnumerator(
        CancellationToken cancellationToken
    )
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
