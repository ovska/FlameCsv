using System.Buffers;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;

namespace FlameCsv;

public readonly struct CsvDialect<T> where T : unmanaged, IEquatable<T>
{
    public required T Delimiter { get; init; }
    public required T Quote { get; init; }
    public required ReadOnlyMemory<T> Newline { get; init; }
    public required ReadOnlyMemory<T> Whitespace { get; init; }
    public required T? Escape { get; init; }

    /// <summary>
    /// Returns search values used to determine whether fields need to be quoted while writing CSV.
    /// </summary>
    public required SearchValues<T> NeedsQuoting
    {
        get => _needsQuoting.Value ?? TryInitializeNeedsQuoting();
        init => _needsQuoting.Value = value;
    }

    private readonly StrongBox<SearchValues<T>?> _needsQuoting = new();

    public CsvDialect()
    {
        Delimiter = default;
        Quote = default;
        Newline = default;
        Whitespace = default;
        Escape = null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Validate()
    {
        List<string>? errors = null;

        T delimiter = Delimiter;
        T quote = Quote;
        T? escape = Escape;
        ReadOnlySpan<T> whitespace = Whitespace.Span;
        ReadOnlySpan<T> newline = Newline.Span;

        if (newline.Length == 0)
        {
            // use crlf if we have a known token type
            if (typeof(T) == typeof(char))
            {
                newline = "\r\n".AsSpan().UnsafeCast<char, T>();
            }
            else if (typeof(T) == typeof(byte))
            {
                newline = "\r\n"u8.UnsafeCast<byte, T>();
            }
            else
            {
                throw new NotSupportedException(
                    $"Automatic newline detection (empty Newline) is not supported for token type {typeof(T)}");
            }
        }

        if (NeedsQuoting is null)
        {
            AddError("NeedsQuoting is null");
        }

        if (delimiter.Equals(quote))
        {
            AddError("Delimiter and Quote must not be equal.");
        }

        if (escape.HasValue)
        {
            if (escape.GetValueOrDefault().Equals(delimiter))
                AddError("Escape must not be equal to Delimiter.");

            if (escape.GetValueOrDefault().Equals(quote))
                AddError("Escape must not be equal to Quote.");
        }


        if (newline.Length is not (1 or 2))
        {
            AddError("Newline must be empty, or 1 or 2 characters long.");
        }
        else
        {
            if (newline.Contains(delimiter))
                AddError("Newline must not contain Delimiter.");

            if (newline.Contains(quote))
                AddError("Newline must not contain Quote.");

            if (escape.HasValue && newline.Contains(escape.GetValueOrDefault()))
                AddError("Newline must not contain Escape.");
        }

        if (!whitespace.IsEmpty)
        {
            if (whitespace.Contains(delimiter))
                AddError("Whitespace must not contain Delimiter.");

            if (whitespace.Contains(quote))
                AddError("Whitespace must not contain Quote.");

            if (escape.HasValue && whitespace.Contains(escape.GetValueOrDefault()))
                AddError("Whitespace must not contain Escape.");

            if (whitespace.IndexOfAny(newline) >= 0)
                AddError("Whitespace must not contain Newline characters.");
        }

        if (errors is not null)
        {
            _needsQuoting.Value = null; // reset possible faulty cached value
            Throw(errors);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        void AddError(string message) => (errors ??= []).Add(message);

        static void Throw(List<string> errors)
        {
            throw new CsvConfigurationException($"Invalid CsvOptions dialect: {string.Join(" ", errors)}");
        }
    }

    private SearchValues<T> TryInitializeNeedsQuoting()
    {
        ValueListBuilder<T> list = new(stackalloc T[8]);
        list.Append(Delimiter);
        list.Append(Quote);

        scoped ReadOnlySpan<T> newlineSpan = Newline.Span;

        if (newlineSpan.IsEmpty)
        {
            if (typeof(T) == typeof(char))
            {
                newlineSpan = "\r\n".AsSpan().UnsafeCast<char, T>();
            }
            else if (typeof(T) == typeof(byte))
            {
                newlineSpan = "\r\n"u8.UnsafeCast<byte, T>();
            }
            else
            {
                throw new NotSupportedException($"Empty newline is not supported for token type {typeof(T).FullName}");
            }
        }

        list.Append(newlineSpan);

        if (Escape.HasValue)
        {
            list.Append(Escape.Value);
        }

        SearchValues<T> result;

        if (typeof(T) == typeof(byte))
        {
            result = (SearchValues<T>)(object)SearchValues.Create(list.AsSpan().UnsafeCast<T, byte>());
        }
        else if (typeof(T) == typeof(char))
        {
            result = (SearchValues<T>)(object)SearchValues.Create(list.AsSpan().UnsafeCast<T, char>());
        }
        else
        {
            throw new NotSupportedException($"NeedsQuoting must be provided for token type {typeof(T).FullName}");
        }

        list.Dispose();
        return _needsQuoting.Value ??= result;
    }
}
