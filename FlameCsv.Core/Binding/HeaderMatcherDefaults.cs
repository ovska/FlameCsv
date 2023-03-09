using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Binding;

public interface ICsvHeaderMatcher<T> where T : unmanaged, IEquatable<T>
{
    CsvBinding<TResult>? TryMatch<TResult>(ReadOnlySpan<T> value, in HeaderBindingArgs args);
}

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

        throw NotSupportedGeneric<T>();
    }

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
            if (data.IsEmpty)
            {
                return ImplInner(default, values.Span, stringComparison);
            }

            int maxLength = Encoding.UTF8.GetMaxCharCount(data.Length);
            int written;

            if (Token<char>.CanStackalloc(maxLength))
            {
                Span<char> buffer = stackalloc char[maxLength];
                written = Encoding.UTF8.GetChars(data, buffer);
                return ImplInner(buffer[..written], values.Span, stringComparison);
            }

            using var spanOwner = SpanOwner<char>.Allocate(maxLength);
            written = Encoding.UTF8.GetChars(data, spanOwner.Span);
            return ImplInner(spanOwner.Span[..written], values.Span, stringComparison);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ImplInner(
            ReadOnlySpan<char> data,
            ReadOnlySpan<string?> values,
            StringComparison stringComparison)
        {
            foreach (var value in values)
            {
                if (data.Equals(value, stringComparison))
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
            $"{nameof(IHeaderBinder<T>)} and use it in {nameof(CsvReaderOptions<T>.HeaderBinder)} in options.");

    }
}
