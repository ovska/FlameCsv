using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Binding;

/// <summary>
/// Built-in callbacks for header binding.
/// </summary>
public static class DefaultHeaderMatchers
{
    /// <summary>
    /// Matches member names to the column using the specified comparison.
    /// </summary>
    /// <param name="stringComparison">Comparison to use</param>
    /// <returns>Function that matches header columns to members</returns>
    public static CsvHeaderMatcher<char> MatchText(StringComparison stringComparison)
    {
        _ = "".Equals("", stringComparison); // validate the parameter
        return Impl;

        CsvBinding? Impl(in HeaderBindingArgs args, ReadOnlySpan<char> data)
        {
            if (data.Equals(args.Value.AsSpan(), stringComparison))
                return new CsvBinding(args.Index, args.Member);

            return default;
        }
    }

    /// <inheritdoc cref="MatchText"/>
    public static CsvHeaderMatcher<byte> MatchUtf8(StringComparison stringComparison)
    {
        _ = "".Equals("", stringComparison); // validate the parameter
        return Impl;

        CsvBinding? Impl(in HeaderBindingArgs args, ReadOnlySpan<byte> data)
        {
            var length = Encoding.UTF8.GetMaxCharCount(data.Length);

            if (length < 64)
            {
                Span<char> buffer = stackalloc char[length];
                var written = Encoding.UTF8.GetChars(data, buffer);
                ReadOnlySpan<char> value = buffer[..written];

                if (value.Equals(args.Value.AsSpan(), stringComparison))
                    return new CsvBinding(args.Index, args.Member);
            }
            else
            {
                using var owner = SpanOwner<char>.Allocate(length);
                var written = Encoding.UTF8.GetChars(data, owner.Span);
                ReadOnlySpan<char> value = owner.Span[..written];

                if (value.Equals(args.Value.AsSpan(), stringComparison))
                    return new CsvBinding(args.Index, args.Member);
            }

            return default;
        }
    }
}
