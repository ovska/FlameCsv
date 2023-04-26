using System.Buffers;
using System.Collections;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv;

public sealed class CsvValueEnumerable<T, TValue> : IEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
{
    public CsvReaderOptions<T> Options { get; }
    public ReadOnlySequence<T> Data { get; }

    public CsvValueEnumerable(CsvReaderOptions<T> options, ReadOnlyMemory<T> csv)
        : this(options, new ReadOnlySequence<T>(csv))
    {
    }

    public CsvValueEnumerable(CsvReaderOptions<T> options, ReadOnlySequence<T> csv)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        Data = csv;
    }

    public IEnumerator<TValue> GetEnumerator()
    {
        Options.MakeReadOnly();

        if (Options.HasHeader)
        {
            return new CsvValueEnumerator<T, TValue, CsvHeaderProcessor<T, TValue>>(
                Data,
                new CsvHeaderProcessor<T, TValue>(Options));
        }
        else
        {
            return new CsvValueEnumerator<T, TValue, CsvProcessor<T, TValue>>(
                Data,
                new CsvProcessor<T, TValue>(new CsvReadingContext<T>(Options), Options.GetMaterializer<T, TValue>()));
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
