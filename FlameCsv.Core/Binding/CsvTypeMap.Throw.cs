using FlameCsv.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Binding;

public abstract partial class CsvTypeMap<T, TValue>
{
    [DoesNotReturn]
    protected void ThrowDuplicate(string member, string field, bool exposeContent)
    {
        if (!exposeContent)
            throw new CsvBindingException<TValue>($"Member '{member}' was matched multiple times.");

        throw new CsvBindingException<TValue>($"Already matched member '{member}' also matched to field '{field}'.");
    }

    [DoesNotReturn]
    protected TryParseHandler? ThrowUnmatched(string field, int index, bool exposeContent)
    {
        if (!exposeContent)
            throw new CsvBindingException<TValue>($"Unmatched header field at index {index}.");

        throw new CsvBindingException<TValue>($"Unmatched header field '{field}' at index {index}.");
    }

    [DoesNotReturn]
    protected void ThrowRequiredNotRead(string member, ReadOnlySpan<string> headers, bool exposeContent)
    {
        if (!exposeContent)
            throw new CsvBindingException<TValue>($"Required member '{member}' was not matched to any header field.");

        throw new CsvBindingException<TValue>(
            $"Required member '{member}' was not matched to any header field: [{FormatHeaders(headers)}]");
    }

    [DoesNotReturn]
    protected void ThrowNoFieldsBound(ReadOnlySpan<string> headers, bool exposeContent)
    {
        if (!exposeContent)
            throw new CsvBindingException<TValue>("No header fields were matched to a member.");

        throw new CsvBindingException<TValue>(
            $"No header fields were matched to a member: [{FormatHeaders(headers)}]");
    }

    private static string FormatHeaders(ReadOnlySpan<string> headers) => string.Join(", ", headers.ToArray().Select(x => $"\"{x}\""));
}
