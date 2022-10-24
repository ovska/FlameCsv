namespace FlameCsv.Binding.Providers;

/// <summary>
/// Matches headers to member names of <typeparamref name="TResult"/>.
/// </summary>
public sealed class HeaderUtf8BindingProvider<TResult> : HeaderBindingProviderBase<byte, TResult>
{
    /// <summary>
    /// Initializes a provider that matches headers to member names.
    /// </summary>
    /// <param name="stringComparison">Comparison used to check the header values</param>
    /// <param name="ignoreNonMatched">Non-matched columns should be ignored</param>
    public HeaderUtf8BindingProvider(
        StringComparison stringComparison = StringComparison.Ordinal,
        bool ignoreNonMatched = false)
        : base(DefaultHeaderMatchers.MatchUtf8(stringComparison), ignoreNonMatched)
    {
    }

    /// <summary>
    /// Initializes a provider that matches headers using the parameter function.
    /// </summary>
    /// <param name="matcher">Function to match the members</param>
    /// <param name="ignoreNonMatched">Non-matched columns should be ignored</param>
    public HeaderUtf8BindingProvider(
        CsvHeaderMatcher<byte> matcher,
        bool ignoreNonMatched = false)
        : base(matcher, ignoreNonMatched)
    {
    }
}
