using FlameCsv.Exceptions;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using FlameCsv.Writing;

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
    /// Returns a materializer for <typeparamref name="TValue"/> bound to CSV header.
    /// </summary>
    public abstract IMaterializer<T, TValue> BindMembers(ReadOnlySpan<string> headers, CsvOptions<T> options);

    /// <summary>
    /// Returns a materializer for <typeparamref name="TValue"/> bound to column indexes.
    /// </summary>
    public abstract IMaterializer<T, TValue> BindMembers(CsvOptions<T> options);

    /// <summary>
    /// Returns a dematerializer for <typeparamref name="TValue"/>.
    /// </summary>
    /// <exception cref="CsvBindingException">
    /// Options is configured not to write a header, but <typeparamref name="TValue"/> has no index binding.
    /// </exception>
    public abstract IDematerializer<T, TValue> GetDematerializer(CsvOptions<T> options);

    [DoesNotReturn]
    protected static void ThrowDuplicate(string member, string field, ReadOnlySpan<string> headers, CsvOptions<T> options)
    {
        if (!options.AllowContentInExceptions)
            throw new CsvBindingException<TValue>($"'{member}' matched to multiple of the {headers.Length} headers.");

        throw new CsvBindingException<TValue>(
            $"\"{member}\" matched to multiple headers, including '{field}' in [{FormatHeaders(headers)}].");
    }

    [DoesNotReturn,]
    protected static void ThrowUnmatched(string field, int index, CsvOptions<T> options)
    {
        if (!options.AllowContentInExceptions)
            throw new CsvBindingException<TValue>($"Unmatched header field at index {index}.");

        throw new CsvBindingException<TValue>($"Unmatched header field '{field}' at index {index}.");
    }

    [DoesNotReturn,]
    protected static void ThrowRequiredNotRead(IEnumerable<string> members, ReadOnlySpan<string> headers, CsvOptions<T> options)
    {
        string missingMembers = string.Join(", ", members.Select(x => $"\"{x}\""));

        if (!options.AllowContentInExceptions)
            throw new CsvBindingException<TValue>($"Required members/parameters [{missingMembers}] were not matched to any header field.");

        throw new CsvBindingException<TValue>(
            $"Required members/parameters [{missingMembers}] were not matched to any header field: [{FormatHeaders(headers)}]");
    }

    [DoesNotReturn]
    protected static void ThrowNoFieldsBound(ReadOnlySpan<string> headers, CsvOptions<T> options)
    {
        if (!options.AllowContentInExceptions)
            throw new CsvBindingException<TValue>("No header fields were matched to a member or parameter.");

        throw new CsvBindingException<TValue>(
            $"No header fields were matched to a member or parameter: [{FormatHeaders(headers)}]");
    }

    private static string FormatHeaders(ReadOnlySpan<string> headers) => string.Join(", ", headers.ToArray().Select(x => $"\"{x}\""));
}
