using CommunityToolkit.HighPerformance;

namespace FlameCsv.Binding;

public sealed class DefaultTextHeaderMatcher : ICsvHeaderMatcher<char>
{
    private readonly StringComparison _comparison;

    public DefaultTextHeaderMatcher(StringComparison comparison)
    {
        _ = "".Equals("", comparison); // validate the parameter
        _comparison = comparison;
    }

    public CsvBinding<TResult>? TryMatch<TResult>(ReadOnlySpan<char> value, in HeaderBindingArgs args)
    {
        return args.Value.AsSpan().Equals(value, _comparison)
            ? CsvBinding.FromHeaderBinding<TResult>(in args)
            : null;
    }
}
