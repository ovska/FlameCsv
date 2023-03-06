namespace FlameCsv.Binding;

/// <summary>
/// Matches headers to member names.
/// </summary>
public sealed class HeaderUtf8Binder : HeaderBinderBase<byte>
{
    /// <summary>
    /// Initializes a provider that matches headers to member names.
    /// </summary>
    /// <param name="stringComparison">Comparison used to check the header values</param>
    /// <param name="ignoreUnmatched">Whether columns that cannot be matched are ignored</param>
    public HeaderUtf8Binder(
        StringComparison stringComparison = HeaderMatcherDefaults.DefaultComparison,
        bool ignoreUnmatched = false)
        : base(HeaderMatcherDefaults.MatchUtf8(stringComparison), ignoreUnmatched)
    {
    }

    /// <summary>
    /// Initializes a provider that matches headers using the parameter function.
    /// </summary>
    /// <param name="matcher">Function to match the members</param>
    /// <param name="ignoreUnmatched">Whether columns that cannot be matched are ignored</param>
    public HeaderUtf8Binder(
        CsvHeaderMatcher<byte> matcher,
        bool ignoreUnmatched = false)
        : base(matcher, ignoreUnmatched)
    {
    }
}
