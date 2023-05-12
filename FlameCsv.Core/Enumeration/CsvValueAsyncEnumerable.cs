using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;

namespace FlameCsv.Enumeration;

[RequiresUnreferencedCode(Messages.CompiledExpressions)]
public sealed class CsvValueAsyncEnumerable<T, [DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue> : IAsyncEnumerable<TValue>
    where T : unmanaged, IEquatable<T>
{
    private readonly CsvReadingContext<T> _context;
    private readonly ICsvPipeReader<T> _reader;

    internal CsvValueAsyncEnumerable(
        ICsvPipeReader<T> reader,
        in CsvReadingContext<T> context)
    {
        _reader = reader;
        _context = context;
    }

    public CsvValueAsyncEnumerator<T, TValue> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return new CsvValueAsyncEnumerator<T, TValue>(in _context, _reader, cancellationToken);
    }

    IAsyncEnumerator<TValue> IAsyncEnumerable<TValue>.GetAsyncEnumerator(CancellationToken cancellationToken)
    {
        return GetAsyncEnumerator(cancellationToken);
    }
}
