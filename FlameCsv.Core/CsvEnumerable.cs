using System.Buffers;
using System.Collections;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

/// <summary>
/// Enumerates known data into CSV records.
/// </summary>
/// <remarks>
/// Maybe be enumerated multiple times, multiple concurrent enumerations are allowed.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public readonly struct CsvEnumerable<T> : IEnumerable<CsvRecord<T>> where T : unmanaged, IEquatable<T>
{
    private readonly ReadOnlySequence<T> _data;
    private readonly CsvReaderOptions<T> _options;

    public CsvEnumerable(ReadOnlyMemory<T> data, CsvReaderOptions<T> options)
        : this(new ReadOnlySequence<T>(data), options)
    {
    }


    public CsvEnumerable(ReadOnlySequence<T> data, CsvReaderOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _data = data;
        _options = options;
    }

    public CsvEnumerator<T> GetEnumerator()
    {
        GuardEx.EnsureNotDefaultStruct(_options);
        return new(_data, _options, CancellationToken.None);
    }

    IEnumerator<CsvRecord<T>> IEnumerable<CsvRecord<T>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Asynchronously reads data from a source into CSV records, use <see cref="CsvReader"/> to create.
/// </summary>
/// <remarks>
/// Maybe NOT be enumerated multiple times, multiple concurrent enumerations are NOT allowed.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
public readonly struct AsyncCsvEnumerable<T> : IAsyncEnumerable<CsvRecord<T>> where T : unmanaged, IEquatable<T>
{
    private readonly ICsvPipeReader<T> _reader;
    private readonly CsvReaderOptions<T> _options;

    internal AsyncCsvEnumerable(ICsvPipeReader<T> reader, CsvReaderOptions<T> options)
    {
        _reader = reader;
        _options = options;
    }


    public AsyncCsvEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        GuardEx.EnsureNotDefaultStruct(_options);
        return new(_reader, _options, cancellationToken);
    }

    IAsyncEnumerator<CsvRecord<T>> IAsyncEnumerable<CsvRecord<T>>.GetAsyncEnumerator(CancellationToken cancellationToken) => GetAsyncEnumerator(cancellationToken);
}
