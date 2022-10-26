namespace FlameCsv.Binding.Providers;

/// <summary>
/// Matches headers to member names of <typeparamref name="TResult"/>.
/// </summary>
public sealed class HeaderTextBindingProvider<TResult> : HeaderBindingProviderBase<char, TResult>
{
    /// <summary>
    /// Initializes a provider that matches headers to member names.
    /// </summary>
    /// <param name="stringComparison">Comparison used to check the header values</param>
    /// <param name="unmatchedBehavior">Behavior if a column cannot be matched</param>
    public HeaderTextBindingProvider(
        StringComparison stringComparison = StringComparison.Ordinal,
        UnmatchedHeaderBindingBehavior unmatchedBehavior = UnmatchedHeaderBindingBehavior.RequireAll)
        : base(DefaultHeaderMatchers.MatchText(stringComparison), unmatchedBehavior)
    {
    }

    /// <summary>
    /// Initializes a provider that matches headers using the parameter function.
    /// </summary>
    /// <param name="matcher">Function to match the members</param>
    /// <param name="unmatchedBehavior">Behavior if a column cannot be matched</param>
    public HeaderTextBindingProvider(
        CsvHeaderMatcher<char> matcher,
        UnmatchedHeaderBindingBehavior unmatchedBehavior = UnmatchedHeaderBindingBehavior.RequireAll)
        : base(matcher, unmatchedBehavior)
    {
    }
}
