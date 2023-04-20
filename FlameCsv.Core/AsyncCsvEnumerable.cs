using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

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

        _options.MakeReadOnly();
    }


    public AsyncCsvEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        GuardEx.EnsureNotDefaultStruct(_options);
        return new(_reader, _options, cancellationToken);
    }

    IAsyncEnumerator<CsvRecord<T>> IAsyncEnumerable<CsvRecord<T>>.GetAsyncEnumerator(CancellationToken cancellationToken)
        => GetAsyncEnumerator(cancellationToken);
}
