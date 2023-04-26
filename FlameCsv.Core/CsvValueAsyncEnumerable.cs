using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

internal sealed class CsvValueAsyncEnumerable<T, TValue, TReader> : IAsyncEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
    where TReader : struct, ICsvPipeReader<T>
{
    private readonly CsvReaderOptions<T> _options;
    private readonly TReader _reader;

    public CsvValueAsyncEnumerable(CsvReaderOptions<T> options, TReader reader)
    {
        _options = options;
        _reader = reader;
    }

    public IAsyncEnumerator<TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        _options.MakeReadOnly();

        if (_options.HasHeader)
        {
            // TODO!!!!
            return new CsvValueAsyncEnumerator<T, TValue, TReader, CsvHeaderProcessor<T, TValue>>(
                _reader,
                new CsvHeaderProcessor<T, TValue>(_options),
                cancellationToken);
        }
        else
        {
            CsvReadingContext<T> context = new(_options);

            return new CsvValueAsyncEnumerator<T, TValue, TReader, CsvProcessor<T, TValue>>(
                _reader,
                new CsvProcessor<T, TValue>(in context, _options.GetMaterializer<T, TValue>()),
                cancellationToken);
        }
    }
}
