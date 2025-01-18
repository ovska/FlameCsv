using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Contains the token configuration for reading and writing CSV.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <seealso cref="CsvOptions{T}.Dialect"/>
[PublicAPI]
public readonly struct CsvDialect<T>() where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc cref="CsvOptions{T}.Delimiter"/>
    public required T Delimiter { get; init; }

    /// <inheritdoc cref="CsvOptions{T}.Quote"/>
    public required T Quote { get; init; }

    /// <inheritdoc cref="CsvOptions{T}.Newline"/>
    public required ReadOnlyMemory<T> Newline { get; init; }

    /// <inheritdoc cref="CsvOptions{T}.Whitespace"/>
    public required ReadOnlyMemory<T> Whitespace { get; init; }

    /// <inheritdoc cref="CsvOptions{T}.Escape"/>
    public required T? Escape { get; init; }

    private readonly LazyValues _lazyValues = new();

    /// <summary>
    /// Returns search values used to determine whether fields need to be quoted while writing CSV.
    /// </summary>
    public SearchValues<T> NeedsQuoting
    {
        get => _lazyValues.NeedsQuoting ??= GetNeedsQuoting();
        init => _lazyValues.NeedsQuoting = value;
    }

    /// <summary>
    /// Returns a <see cref="SearchValues{T}"/> instance used to find the next token in CSV.
    /// This includes the delimiter, quote, escape (if applicable), and the first newline token if
    /// <paramref name="length"/> is not 0.
    /// </summary>
    /// <param name="length">
    /// Newline length of the current read operation.
    /// Must be either the same as <see cref="Newline"/> length,
    /// or 0 if reading the final block without a detected newline.
    /// </param>
    /// <returns>Cached instance that can be used to seek the CSV.</returns>
    internal SearchValues<T> GetFindToken(int length)
    {
        return _lazyValues.FindArray[length] ??= InitializeFindToken(length);
    }

    /// <summary>
    /// Determines if all tokens in the dialect are within ASCII range.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if all tokens are within ASCII range.
    /// </returns>
    /// <remarks>Required for vectorization.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool IsAscii => _lazyValues.IsAscii ??= GetIsAscii();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NewlineBuffer<T> GetNewlineOrDefault()
    {
        return Newline.IsEmpty ? NewlineBuffer<T>.CRLF : new NewlineBuffer<T>(Newline.Span);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SearchValues<T> GetNeedsQuoting()
    {
        Throw.IfDefaultStruct(_lazyValues is null, typeof(CsvDialect<T>));

        using ValueListBuilder<T> list = new(stackalloc T[8]);

        list.Append(Delimiter);
        list.Append(Quote);

        if (Newline.IsEmpty)
        {
            list.Append(NewlineBuffer<T>.CRLF.First);
            list.Append(NewlineBuffer<T>.CRLF.Second);
        }
        else
        {
            list.Append(Newline.Span);
        }

        if (Escape.HasValue)
        {
            list.Append(Escape.Value);
        }

        return ToSearchValues(list.AsSpan());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private SearchValues<T> InitializeFindToken(int length)
    {
        Throw.IfDefaultStruct(_lazyValues is null, typeof(CsvDialect<T>));

        using ValueListBuilder<T> list = new(stackalloc T[8]);

        list.Append(Delimiter);
        list.Append(Quote);

        if (Escape.HasValue)
        {
            list.Append(Escape.Value);
        }

        if (length == 0)
        {
            if (!Newline.IsEmpty)
                Throw.Unreachable("Newline length is 0, but Newline is not empty.");
        }
        else
        {
            if (!Newline.IsEmpty)
            {
                list.Append(Newline.Span[0]);
            }
            else if (length == 1)
            {
                list.Append(NewlineBuffer<T>.LF.First);
            }
            else if (length == 2)
            {
                list.Append(NewlineBuffer<T>.CRLF.First);
            }
        }

        return ToSearchValues(list.AsSpan());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool GetIsAscii()
    {
        bool retVal = false;
        ValueListBuilder<T> list = new(stackalloc T[8]);

        list.Append(Delimiter);
        list.Append(Quote);
        if (Escape.HasValue) list.Append(Escape.Value);
        if (!Newline.IsEmpty) list.Append(Newline.Span); // empty newline is CRLF -> always ASCII
        if (!Whitespace.IsEmpty) list.Append(Whitespace.Span);

        if (Unsafe.SizeOf<T>() == sizeof(byte))
        {
            retVal = Ascii.IsValid(list.AsSpan().UnsafeCast<T, byte>());
        }

        if (Unsafe.SizeOf<T>() == sizeof(char))
        {
            retVal = Ascii.IsValid(list.AsSpan().UnsafeCast<T, char>());
        }

        list.Dispose();
        return retVal;
    }

    private static SearchValues<T> ToSearchValues(ReadOnlySpan<T> tokens)
    {
        if (typeof(T) == typeof(byte))
        {
            return (SearchValues<T>)(object)SearchValues.Create(tokens.UnsafeCast<T, byte>());
        }

        if (typeof(T) == typeof(char))
        {
            return (SearchValues<T>)(object)SearchValues.Create(tokens.UnsafeCast<T, char>());
        }

        throw new NotSupportedException($"SearchValues cannot be created for token {typeof(T).FullName}");
    }

    private sealed class LazyValues
    {
        public SearchValues<T>? NeedsQuoting;
        public FindTokens FindArray;
        public bool? IsAscii;

        public void Reset()
        {
            NeedsQuoting = null;
            IsAscii = null;
            FindArray = default;
        }

        [InlineArray(3)]
        public struct FindTokens
        {
            public SearchValues<T>? elem0;
        }
    }

    /// <summary>
    /// Ensures that the dialect is valid, and has no problematic overlap in the tokens.
    /// </summary>
    /// <exception cref="CsvConfigurationException"></exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Validate()
    {
        Throw.IfDefaultStruct(_lazyValues is null, typeof(CsvDialect<T>));

        StringScratch scratch = default;
        using ValueListBuilder<string> errors = new(scratch);

        T delimiter = Delimiter;
        T quote = Quote;
        T? escape = Escape;
        scoped ReadOnlySpan<T> whitespace = Whitespace.Span;
        scoped ReadOnlySpan<T> newline = Newline.IsEmpty
            ? [T.CreateChecked('\r'), T.CreateChecked('\n')]
            : Newline.Span;

        if (delimiter.Equals(quote))
        {
            errors.Append("Delimiter and Quote must not be equal.");
        }

        if (escape.HasValue)
        {
            if (escape.GetValueOrDefault().Equals(delimiter))
                errors.Append("Escape must not be equal to Delimiter.");

            if (escape.GetValueOrDefault().Equals(quote))
                errors.Append("Escape must not be equal to Quote.");
        }

        if (newline.Length is not (1 or 2))
        {
            errors.Append("Newline must be empty, or 1 or 2 characters long.");
        }
        else
        {
            if (newline.Contains(delimiter))
                errors.Append("Newline must not contain Delimiter.");

            if (newline.Contains(quote))
                errors.Append("Newline must not contain Quote.");

            if (escape.HasValue && newline.Contains(escape.GetValueOrDefault()))
                errors.Append("Newline must not contain Escape.");
        }

        if (!whitespace.IsEmpty)
        {
            if (whitespace.Contains(delimiter))
                errors.Append("Whitespace must not contain Delimiter.");

            if (whitespace.Contains(quote))
                errors.Append("Whitespace must not contain Quote.");

            if (escape.HasValue && whitespace.Contains(escape.GetValueOrDefault()))
                errors.Append("Whitespace must not contain Escape.");

            if (whitespace.IndexOfAny(newline) >= 0)
                errors.Append("Whitespace must not contain Newline characters.");
        }

        // otherwise valid, but invalid tokens for utf8
        if (errors.Length == 0 && Unsafe.SizeOf<T>() == sizeof(byte) && !GetIsAscii())
        {
            errors.Append("All tokens for byte must be valid ASCII characters.");
        }

        if (errors.Length != 0)
        {
            _lazyValues.Reset(); // reset possible faulty cached value

            if (Unsafe.SizeOf<T>() is sizeof(byte) or sizeof(char))
            {
                var vsb = new ValueStringBuilder(stackalloc char[64]);
                vsb.Append("Tokens:");
                SingleToken(ref vsb, "Delimiter", Delimiter);
                SingleToken(ref vsb, "Quote", Quote);
                SingleToken(ref vsb, "Escape", escape);
                MultiToken(ref vsb, "Newline", newline);
                MultiToken(ref vsb, "Whitespace", whitespace);
                errors.Append(vsb.ToString());
            }

            InvalidDialect.Throw(errors.AsSpan());
        }

        static void SingleToken(ref ValueStringBuilder vsb, ReadOnlySpan<char> name, T? value)
        {
            vsb.Append(' ');
            vsb.Append(name);
            vsb.Append(": ");
            AppendToken(ref vsb, value);
        }

        static void MultiToken(ref ValueStringBuilder vsb, ReadOnlySpan<char> name, ReadOnlySpan<T> values)
        {
            vsb.Append(' ');
            vsb.Append(name);
            vsb.Append(": ");

            if (values.IsEmpty)
            {
                vsb.Append("<empty>");
                return;
            }

            vsb.Append('[');

            for (int i = 0; i < values.Length; i++)
            {
                AppendToken(ref vsb, values[i]);
            }

            vsb.Append(']');
        }

        static void AppendToken(ref ValueStringBuilder vsb, T? value)
        {
            if (value is null)
            {
                vsb.Append("<null>");
                return;
            }

            char v = (char)ushort.CreateTruncating(value.Value);

            switch (v)
            {
                case '\0': vsb.Append(@"\0"); break;
                case '\\': vsb.Append(@"\\"); break;
                case '\r': vsb.Append(@"\r"); break;
                case '\n': vsb.Append(@"\n"); break;
                case '\t': vsb.Append(@"\t"); break;
                case ' ': vsb.Append(" "); break;
                default: vsb.Append(v); break;
            }
        }
    }
}

// throwhelper doesn't need to be generic
file static class InvalidDialect
{
    [DoesNotReturn]
    [StackTraceHidden]
    public static void Throw(scoped ReadOnlySpan<string> errors)
    {
        throw new CsvConfigurationException($"Invalid CsvOptions dialect: {string.Join(" ", errors.ToArray())}");
    }
}
