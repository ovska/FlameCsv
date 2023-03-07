using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Binding;

/// <summary>
/// Built-in callbacks for header binding.
/// </summary>
internal static class HeaderMatcherDefaults
{
    public const StringComparison DefaultComparison = StringComparison.OrdinalIgnoreCase;

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

        throw new NotSupportedException(
            $"Default header binding for token {typeof(T).ToTypeString()} is not supported. Implement " +
            $"{nameof(IHeaderBinder<T>)} and use it in {nameof(CsvReaderOptions<T>.HeaderBinder)} in options.");
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

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        CsvBinding? Impl(ReadOnlySpan<char> data, in HeaderBindingArgs args)
        {
            return args.Value.AsSpan().Equals(data, stringComparison)
                ? CsvBinding.ForMember(args.Index, args.Member)
                : null;
        }
    }

    /// <inheritdoc cref="MatchText"/>
    public static CsvHeaderMatcher<byte> MatchUtf8(StringComparison stringComparison)
    {
        _ = "".Equals("", stringComparison); // validate the parameter
        return Impl;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        CsvBinding? Impl(ReadOnlySpan<byte> data, in HeaderBindingArgs args)
        {
            int length = Encoding.UTF8.GetMaxCharCount(data.Length);

            if (Token<byte>.CanStackalloc(length))
            {
                return ImplInner(data, stackalloc char[length], in args, stringComparison);
            }
            else
            {
                using var owner = SpanOwner<char>.Allocate(length);
                return ImplInner(data, owner.Span, in args, stringComparison);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static CsvBinding? ImplInner(
            ReadOnlySpan<byte> data,
            Span<char> buffer,
            in HeaderBindingArgs args,
            StringComparison stringComparison)
        {
            var written = Encoding.UTF8.GetChars(data, buffer);

            return args.Value.AsSpan().Equals(buffer[..written], stringComparison)
                ? CsvBinding.ForMember(args.Index, args.Member)
                : null;
        }
    }
}
