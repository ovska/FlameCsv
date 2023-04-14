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
                foreach (var value in values.Span)
                {
                    if (string.IsNullOrEmpty(value))
                        return true;
                }

                return false;
            }

            int maxLength = Encoding.UTF8.GetMaxCharCount(data.Length);

            if (Token<char>.CanStackalloc(maxLength))
            {
                return ImplInner(data, stackalloc char[maxLength], values.Span, stringComparison);
            }

            using var spanOwner = SpanOwner<char>.Allocate(maxLength);
            return ImplInner(data, spanOwner.Span, values.Span, stringComparison);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ImplInner(
            scoped ReadOnlySpan<byte> data,
            scoped Span<char> buffer,
            scoped ReadOnlySpan<string?> values,
            StringComparison stringComparison)
        {
            int written = Encoding.UTF8.GetChars(data, buffer);
            ReadOnlySpan<char> text = buffer[..written];

            foreach (var value in values)
            {
                if (text.Equals(value, stringComparison))
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
