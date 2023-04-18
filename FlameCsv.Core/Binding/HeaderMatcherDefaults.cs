using CommunityToolkit.Diagnostics;
using FlameCsv.Extensions;

namespace FlameCsv.Binding;

/// <summary>
/// Built-in callbacks for header binding.
/// </summary>
internal static class HeaderMatcherDefaults
{
    public const StringComparison DefaultComparison = StringComparison.OrdinalIgnoreCase;

    public static SpanPredicate<T> CheckIgnore<T>(
        ReadOnlyMemory<string?> values,
        StringComparison stringComparison)
        where T : unmanaged, IEquatable<T>
    {
        if (typeof(T) == typeof(char))
        {
            return (SpanPredicate<T>)(object)CheckIgnoreText(values, stringComparison);
        }

        if (typeof(T) == typeof(byte))
        {
            return (SpanPredicate<T>)(object)CheckIgnoreUtf8(values, stringComparison);
        }

        throw NotSupportedGeneric<T>();
    }

    public static SpanPredicate<char> CheckIgnoreText(
        ReadOnlyMemory<string?> values,
        StringComparison stringComparison)
    {
        if (values.IsEmpty)
            return _ => false;

        return Impl;

        bool Impl(ReadOnlySpan<char> data)
        {
            foreach (var value in values.Span)
            {
                if (data.Equals(value, stringComparison))
                    return true;
            }

            return false;
        }
    }

    public static SpanPredicate<byte> CheckIgnoreUtf8(
        ReadOnlyMemory<string?> values,
        StringComparison stringComparison)
    {
        if (values.IsEmpty)
            return _ => false;

        return Impl;

        bool Impl(ReadOnlySpan<byte> data)
        {
            foreach (var value in values.Span)
            {
                if (Utf8Util.SequenceEqual(data, value, stringComparison))
                    return true;
            }

            return false;
        }
    }

    private static Exception NotSupportedGeneric<T>()
        where T : unmanaged, IEquatable<T>
    {
        throw new NotSupportedException(
            $"Default header binding for token {typeof(T).ToTypeString()} is not supported. Implement " +
            $"a custom {nameof(IHeaderBinder<T>)}.");

    }
}
