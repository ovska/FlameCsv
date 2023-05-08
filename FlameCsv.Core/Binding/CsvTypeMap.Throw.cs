using FlameCsv.Exceptions;
using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Binding;

public abstract partial class CsvTypeMap<T, TValue>
{
    [DoesNotReturn]
    protected void ThrowDuplicate(string member, string field, ReadOnlySpan<string> headers, bool exposeContent)
    {
        if (!exposeContent)
            throw new CsvBindingException<TValue>($"Member '{member}' was matched multiple times out of {headers.Length} headers.");

        throw new CsvBindingException<TValue>(
            $"Already matched member '{member}' also matched to field '{field}' in headers [{FormatHeaders(headers)}].");
    }

    [DoesNotReturn]
    protected void ThrowUnmatched(string field, int index, bool exposeContent)
    {
        if (!exposeContent)
            throw new CsvBindingException<TValue>($"Unmatched header field at index {index}.");

        throw new CsvBindingException<TValue>($"Unmatched header field '{field}' at index {index}.");
    }

    [DoesNotReturn]
    protected void ThrowRequiredNotRead(IEnumerable<string> members, ReadOnlySpan<string> headers, bool exposeContent)
    {
        string missingMembers = string.Join(", ", members.Select(x => $"\"{x}\""));

        if (!exposeContent)
            throw new CsvBindingException<TValue>($"Required members [{missingMembers}] were not matched to any header field.");

        throw new CsvBindingException<TValue>(
            $"Required members [{missingMembers}] were not matched to any header field: [{FormatHeaders(headers)}]");
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
