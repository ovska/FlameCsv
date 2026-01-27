using System.Diagnostics;

namespace FlameCsv.Writing.Escaping;

/// <summary>
/// Quoter for <see cref="CsvFieldQuoting.Never"/>.
/// </summary>
internal sealed class NoOpQuoter<T> : IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public static NoOpQuoter<T> Instance { get; } = new NoOpQuoter<T>();

    private NoOpQuoter() { }

    public T Quote => throw new UnreachableException();

    public QuotingResult NeedsQuoting(ReadOnlySpan<T> field) => default;

    public QuotingResult NeedsQuoting(ReadOnlySpan<char> field) => default;
}
