namespace FlameCsv.Binding;

/// <summary>
/// Matches CSV header record values to type bindings.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public interface ICsvHeaderMatcher<T> where T : unmanaged, IEquatable<T>
{
    CsvBinding<TResult>? TryMatch<TResult>(ReadOnlySpan<T> value, in HeaderBindingArgs args);
}
