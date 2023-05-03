using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using FlameCsv.Runtime;

namespace FlameCsv;

[RequiresUnreferencedCode(Trimming.CompiledExpressions)]
internal sealed class CsvValueAsyncEnumerable<T, [DynamicallyAccessedMembers(Trimming.ReflectionBound)] TValue, TReader> : IAsyncEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
    where TReader : struct, ICsvPipeReader<T>
{
    private readonly CsvReadingContext<T> _context;
    private readonly TReader _reader;

    public CsvValueAsyncEnumerable(
        TReader reader,
        in CsvReadingContext<T> context)
    {
        _reader = reader;
        _context = context;
    }

    public IAsyncEnumerator<TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        if (_context.HasHeader)
        {
            return new CsvValueAsyncEnumerator<T, TValue, TReader, CsvHeaderProcessor<T, TValue>>(
                _reader,
                new CsvHeaderProcessor<T, TValue>(in _context),
                cancellationToken);
        }
        else
        {
            return new CsvValueAsyncEnumerator<T, TValue, TReader, CsvProcessor<T, TValue>>(
                _reader,
                new CsvProcessor<T, TValue>(in _context, _context.Options.GetMaterializer<T, TValue>()),
                cancellationToken);
        }
    }
}
