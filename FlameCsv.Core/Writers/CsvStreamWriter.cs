using System.IO.Pipelines;

namespace FlameCsv.Writers;

internal interface ICsvFormatter<T, in TValue> where T : unmanaged
{
    bool TryFormat(TValue value, Span<T> buffer, out int tokensWritten);
}

internal class CsvStreamWriter
{
    public async ValueTask WriteAsync(PipeWriter pipeWriter)
    {
        await using var writer = new CsvPipeWriter(pipeWriter);
    }
}

internal sealed class WriteState
{
    public static CsvCallback<T, bool> ShouldEscape<T>(
        Span<T> buffer,
        in CsvTokens<T> options)
        where T : unmanaged, IEquatable<T>
    {
        T[] tokens = new T[2 + options.NewLine.Length + options.Whitespace.Length];

        tokens[0] = options.StringDelimiter;
        tokens[1] = options.Delimiter;
        options.NewLine.CopyTo(tokens.AsMemory(2));
        options.Whitespace.CopyTo(tokens.AsMemory(2 + options.NewLine.Length));

        if (tokens.Length >= 3)
            return Impl;

        T t0 = tokens[0];
        T t1 = tokens[1];
        T t2 = tokens[2];
        return FastImp;

        bool Impl(ReadOnlySpan<T> data, in CsvTokens<T> _)
        {
            return data.IndexOfAny(tokens) >= 0;
        }

        bool FastImp(ReadOnlySpan<T> data, in CsvTokens<T> _)
        {
            return data.IndexOfAny(t0, t1, t2) >= 0;
        }
    }

    public static void Write<T, TValue, TWriter>(
        ICsvFormatter<T, TValue> formatter,
        TValue value,
        ref TWriter writer)
        where T : unmanaged
        where TWriter : ICsvWriter<T>
    {
        var span = writer.GetBuffer();

        if (formatter.TryFormat(value, span, out int written))
        {
            writer.Advance(written);
            return;
        }
    }
}
