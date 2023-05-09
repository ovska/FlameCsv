using FlameCsv.Exceptions;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;

namespace FlameCsv.Binding;

/// <summary>
/// Provides compile-time mapping to parse <typeparamref name="TValue"/> records from CSV.
/// </summary>
/// <remarks>
/// Decorate a non-generic <see langword="partial"/> <see langword="class"/> with <see cref="CsvTypeMapAttribute{T, TValue}"/>
/// to generate the implementation.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Record type</typeparam>
public abstract class CsvTypeMap<T, TValue> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns bindings for <typeparamref name="TValue"/> for the headers.
    /// </summary>
    protected abstract IMaterializer<T, TValue> BindMembers(
        ReadOnlySpan<string> headers,
        bool exposeContent,
        CsvReaderOptions<T> options);

    /// <summary>
    /// Returns bindings for <typeparamref name="TValue"/> for indexes.
    /// </summary>
    protected abstract IMaterializer<T, TValue> BindMembers(
        bool exposeContent,
        CsvReaderOptions<T> options);

    internal IMaterializer<T, TValue> GetMaterializer(in CsvReadingContext<T> context)
    {
        return BindMembers(context.ExposeContent, context.Options);
    }

    internal IMaterializer<T, TValue> GetMaterializer(ReadOnlySpan<string> headers, in CsvReadingContext<T> context)
    {
        return BindMembers(headers, context.ExposeContent, context.Options);
    }

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
