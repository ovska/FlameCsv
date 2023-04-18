namespace FlameCsv.Configuration;

public interface ICsvStringConfiguration<T> where T : unmanaged, IEquatable<T>
{
    // todo: simplify
    StringComparison Comparison { get; }
    bool TokensEqual(ReadOnlySpan<T> tokens, ReadOnlySpan<char> chars);
    string GetTokensAsString(ReadOnlySpan<T> tokens);
}
