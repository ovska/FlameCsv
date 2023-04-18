using FlameCsv.Extensions;

namespace FlameCsv.Binding;

public sealed class DefaultUtf8HeaderMatcher : ICsvHeaderMatcher<byte>
{
    private readonly StringComparison _comparison;

    public DefaultUtf8HeaderMatcher(StringComparison comparison)
    {
        _ = ReadOnlySpan<char>.Empty.Equals(default, comparison); // validate the parameter
        _comparison = comparison;
    }

    public CsvBinding<TResult>? TryMatch<TResult>(ReadOnlySpan<byte> value, in HeaderBindingArgs args)
    {
        return Utf8Util.SequenceEqual(value, args.Value, _comparison)
            ? CsvBinding.FromHeaderBinding<TResult>(in args)
            : null;
    }
}
