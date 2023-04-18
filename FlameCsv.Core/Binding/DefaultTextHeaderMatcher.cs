namespace FlameCsv.Binding;

public sealed class DefaultTextHeaderMatcher : ICsvHeaderMatcher<char>
{
    private readonly StringComparison _comparison;

    public DefaultTextHeaderMatcher(StringComparison comparison)
    {
        _ = ReadOnlySpan<char>.Empty.Equals(default, comparison); // validate the parameter
        _comparison = comparison;
    }

    public CsvBinding<TResult>? TryMatch<TResult>(ReadOnlySpan<char> value, in HeaderBindingArgs args)
    {
        return value.Equals(args.Value, _comparison)
            ? CsvBinding.FromHeaderBinding<TResult>(in args)
            : null;
    }
}
