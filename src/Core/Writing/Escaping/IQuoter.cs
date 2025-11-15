using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FlameCsv.Writing.Escaping;

internal record struct QuotingResult(bool NeedsQuoting, int SpecialCount);

internal interface IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    T Quote { get; }
    QuotingResult NeedsQuoting(ReadOnlySpan<T> field);
    QuotingResult NeedsQuoting(ReadOnlySpan<char> field);
}

/// <summary>
/// Quoter for <see cref="CsvFieldQuoting.Never"/>.
/// </summary>
internal sealed class NoOpQuoter<T> : IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public static NoOpQuoter<T> Instance { get; } = new NoOpQuoter<T>();

    private NoOpQuoter() { }

    public T Quote => throw new UnreachableException();

    public QuotingResult NeedsQuoting(ReadOnlySpan<T> field) => default;

    public QuotingResult NeedsQuoting(ReadOnlySpan<char> field) => default;
}

internal sealed class AlwaysQuoter<T>(T quote) : IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public QuotingResult NeedsQuoting(ReadOnlySpan<T> field)
    {
        return new QuotingResult(true, field.Count(quote));
    }

    public QuotingResult NeedsQuoting(ReadOnlySpan<char> field)
    {
        return new QuotingResult(true, field.Count((char)ushort.CreateTruncating(quote)));
    }

    public T Quote => quote;
}

internal static class Quoter
{
    public static IQuoter<T> Create<T>(CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        // optimize for the default common case
        if (options.FieldQuoting is CsvFieldQuoting.Auto)
        {
            return new AutoQuoter<T>(options);
        }

        if (options.FieldQuoting is CsvFieldQuoting.Never)
        {
            return NoOpQuoter<T>.Instance;
        }

        if (options.FieldQuoting is CsvFieldQuoting.Always)
        {
            return new AlwaysQuoter<T>(T.CreateTruncating(options.Quote));
        }

        return new Quoter<T>(options);
    }
}

internal sealed class AutoQuoter<T> : IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public T Quote { get; }

    private readonly SearchValues<T> _needsQuoting;
    private readonly SearchValues<char> _needsQuotingChar;

    public AutoQuoter(CsvOptions<T> options)
    {
        Quote = T.CreateTruncating(options.Quote);
        _needsQuoting = options.NeedsQuoting;
        _needsQuotingChar = options.NeedsQuotingChar;
    }

    public QuotingResult NeedsQuoting(ReadOnlySpan<T> field)
    {
        int index = field.IndexOfAny(_needsQuoting);

        if (index >= 0)
        {
            int length = field.Length - index;
            ReadOnlySpan<T> sliced = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref MemoryMarshal.GetReference(field), (uint)index),
                length
            );
            return new QuotingResult(true, sliced.Count(Quote));
        }

        return default;
    }

    public QuotingResult NeedsQuoting(ReadOnlySpan<char> field)
    {
        int index = field.IndexOfAny(_needsQuotingChar);

        if (index >= 0)
        {
            int length = field.Length - index;
            ReadOnlySpan<char> sliced = MemoryMarshal.CreateReadOnlySpan(
                ref Unsafe.Add(ref MemoryMarshal.GetReference(field), (uint)index),
                length
            );
            return new QuotingResult(true, sliced.Count((char)ushort.CreateTruncating(Quote)));
        }

        return default;
    }
}

internal sealed class Quoter<T> : IQuoter<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly SearchValues<T> _needsQuoting;
    private readonly SearchValues<char> _needsQuotingChar;
    private readonly bool _empty;
    private readonly bool _auto;
    private readonly bool _leading;
    private readonly bool _trailing;

    public Quoter(CsvOptions<T> options)
    {
        _needsQuoting = options.NeedsQuoting;
        _needsQuotingChar = options.NeedsQuotingChar;
        Quote = T.CreateTruncating(options.Quote);
        _empty = (options.FieldQuoting & CsvFieldQuoting.Empty) != 0;
        _auto = (options.FieldQuoting & CsvFieldQuoting.Auto) != 0;
        _leading = (options.FieldQuoting & CsvFieldQuoting.LeadingSpaces) != 0;
        _trailing = (options.FieldQuoting & CsvFieldQuoting.TrailingSpaces) != 0;
    }

    public T Quote { get; }

    public QuotingResult NeedsQuoting(ReadOnlySpan<T> field)
    {
        if (field.IsEmpty)
        {
            return new QuotingResult(_empty, 0);
        }

        bool retVal = false;

        // range for the count scan
        int start = 0;
        int length = field.Length;

        if (_leading && field[0] == T.CreateTruncating(' '))
        {
            retVal = true;
            start++;
            length--;
        }

        if (_trailing && !retVal && field[^1] == T.CreateTruncating(' '))
        {
            retVal = true;
            length--;
        }

        if (_auto && !retVal)
        {
            int index = field.IndexOfAny(_needsQuoting);

            if (index >= 0)
            {
                retVal = true;
                start = index;
                length = field.Length - index;
            }
        }

        if (retVal)
        {
            int quoteCount = MemoryMarshal
                .CreateReadOnlySpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(field), (uint)start), length)
                .Count(Quote);

            return new QuotingResult(true, quoteCount);
        }

        return default;
    }

    public QuotingResult NeedsQuoting(ReadOnlySpan<char> field)
    {
        if (field.IsEmpty)
        {
            return new QuotingResult(_empty, 0);
        }

        bool retVal = false;

        // range for the count scan
        int start = 0;
        int length = field.Length;

        if (_leading && field[0] == ' ')
        {
            retVal = true;
            start++;
            length--;
        }

        if (_trailing && !retVal && field[^1] == ' ')
        {
            retVal = true;
            length--;
        }

        if (_auto && !retVal)
        {
            int index = field.IndexOfAny(_needsQuotingChar);

            if (index >= 0)
            {
                retVal = true;
                start = index;
                length = field.Length - index;
            }
        }

        if (retVal)
        {
            int quoteCount = MemoryMarshal
                .CreateReadOnlySpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(field), (uint)start), length)
                .Count((char)ushort.CreateTruncating(Quote));

            return new QuotingResult(true, quoteCount);
        }

        return default;
    }
}
