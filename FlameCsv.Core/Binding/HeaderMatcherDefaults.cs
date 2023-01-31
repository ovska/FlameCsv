using System.Text;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Binding;

/// <summary>
/// Built-in callbacks for header binding.
/// </summary>
internal static class HeaderMatcherDefaults
{
    private static readonly HeaderTextBinder _headerTextBinder = new();
    private static readonly HeaderUtf8Binder _headerUtf8Binder = new();

    /// <summary>
    /// Returns the default header matched for the token type.
    /// </summary>
    /// <seealso cref="HeaderTextBinder"/>
    /// <seealso cref="_headerUtf8Binder"/>
    internal static IHeaderBinder<T> GetBinder<T>() where T : unmanaged, IEquatable<T>
    {
        if (typeof(T) == typeof(char))
            return (IHeaderBinder<T>)(object)_headerTextBinder;

        if (typeof(T) == typeof(byte))
            return (IHeaderBinder<T>)(object)_headerUtf8Binder;

        throw new NotSupportedException($"Default header binding for {typeof(T)} is not supported.");
    }


    /// <summary>
    /// Matches member names to the column using the specified comparison.
    /// </summary>
    /// <param name="stringComparison">Comparison to use</param>
    /// <returns>Function that matches header columns to members</returns>
    public static CsvHeaderMatcher<char> MatchText(StringComparison stringComparison)
    {
        _ = "".Equals("", stringComparison); // validate the parameter
        return Impl;

        CsvBinding? Impl(ReadOnlySpan<char> data, in HeaderBindingArgs args)
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

        CsvBinding? Impl(ReadOnlySpan<byte> data, in HeaderBindingArgs args)
        {
            int length = Encoding.UTF8.GetMaxCharCount(data.Length);

            if (Token<byte>.CanStackalloc(length))
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
