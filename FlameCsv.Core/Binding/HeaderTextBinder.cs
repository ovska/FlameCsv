namespace FlameCsv.Binding;

/// <summary>
/// Matches headers to member names.
/// </summary>
public sealed class HeaderTextBinder : HeaderBinderBase<char>
{
    /// <summary>
    /// Initializes a provider that matches headers to member names.
    /// </summary>
    /// <param name="stringComparison">Comparison used to check the header values</param>
    /// <param name="ignoreUnmatched">Whether columns that cannot be matched are ignored</param>
    public HeaderTextBinder(
        StringComparison stringComparison = StringComparison.OrdinalIgnoreCase,
        bool ignoreUnmatched = false)
        : base(HeaderMatcherDefaults.MatchText(stringComparison), ignoreUnmatched)
    {
    }

    /// <summary>
    /// Initializes a provider that matches headers using the parameter function.
    /// </summary>
    /// <param name="matcher">Function to match the members</param>
    /// <param name="ignoreUnmatched">Whether columns that cannot be matched are ignored</param>
    public HeaderTextBinder(
        CsvHeaderMatcher<char> matcher,
        bool ignoreUnmatched = false)
        : base(matcher, ignoreUnmatched)
    {
    }
}
