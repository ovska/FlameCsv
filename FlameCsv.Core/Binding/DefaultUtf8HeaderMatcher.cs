using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Binding;

public sealed class DefaultUtf8HeaderMatcher : ICsvHeaderMatcher<byte>
{
    private readonly StringComparison _comparison;

    public DefaultUtf8HeaderMatcher(StringComparison comparison)
    {
        _ = "".Equals("", comparison); // validate the parameter
        _comparison = comparison;
    }

    public CsvBinding<TResult>? TryMatch<TResult>(ReadOnlySpan<byte> value, in HeaderBindingArgs args)
    {
        int length = Encoding.UTF8.GetMaxCharCount(value.Length);

        if (Token<byte>.CanStackalloc(length))
        {
            return Impl<TResult>(value, stackalloc char[length], in args, _comparison);
        }
        else
        {
            using var owner = SpanOwner<char>.Allocate(length);
            return Impl<TResult>(value, owner.Span, in args, _comparison);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CsvBinding<TResult>? Impl<TResult>(
        ReadOnlySpan<byte> data,
        scoped Span<char> buffer,
        in HeaderBindingArgs args,
        StringComparison stringComparison)
    {
        var written = Encoding.UTF8.GetChars(data, buffer);

        return args.Value.AsSpan().Equals(buffer[..written], stringComparison)
            ? CsvBinding.FromHeaderBinding<TResult>(in args)
            : null;
    }
}
