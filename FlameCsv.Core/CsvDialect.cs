using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NewlineBuffer<T> where T : unmanaged, IBinaryInteger<T>
{
    public static readonly NewlineBuffer<T> CRLF = new(2, T.CreateChecked('\r'), T.CreateChecked('\n'));
    public static readonly NewlineBuffer<T> LF = new(1, T.CreateChecked('\n'), T.CreateChecked('\n'));

    public readonly int Length;
    public readonly T First;
    public readonly T Second;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NewlineBuffer(scoped ReadOnlySpan<T> values)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Length, 2);
        First = values[0];
        Second = values.Length == 1 ? values[0] : values[1];
        Length = values.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private NewlineBuffer(int length, T first, T second)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)length, 2u);
        First = first;
        Second = second;
        Length = length;
    }
}

/// <summary>
/// Contains the token configuration for reading and writing CSV.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <seealso cref="CsvOptions{T}.Dialect"/>
[PublicAPI]
public readonly struct CsvDialect<T> where T : unmanaged, IBinaryInteger<T>
{
    public required T Delimiter { get; init; }
    public required T Quote { get; init; }
    public required ReadOnlyMemory<T> Newline { get; init; }
    public required ReadOnlyMemory<T> Whitespace { get; init; }
    public required T? Escape { get; init; }

    /// <summary>
    /// Returns search values used to determine whether fields need to be quoted while writing CSV.
    /// </summary>
    public SearchValues<T> NeedsQuoting
    {
        get => _needsQuoting?.Value ?? TryInitializeNeedsQuoting();
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
        scoped ReadOnlySpan<T> whitespace = Whitespace.Span;
        scoped ReadOnlySpan<T> newline = Newline.IsEmpty
            ? [T.CreateChecked('\r'), T.CreateChecked('\n')]
            : Newline.Span;

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
        Throw.IfDefaultStruct(_needsQuoting is null, typeof(CsvDialect<T>));

        ValueListBuilder<T> list = new(stackalloc T[5]);
        list.Append(Delimiter);
        list.Append(Quote);
        list.Append(
            Newline.IsEmpty
                ? MemoryMarshal.CreateReadOnlySpan(in NewlineBuffer<T>.CRLF.First, 2)
                : Newline.Span);
        list.Append(Escape.HasValue ? [Escape.Value] : []);

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
