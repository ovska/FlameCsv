using FlameCsv.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;
using FlameCsv.Utilities;
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
public abstract class CsvTypeMap<T, TValue> : CsvTypeMap where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc/>
    protected sealed override Type TargetType => typeof(TValue);

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
}

/// <summary>
/// Base class providing throw helpers for <see cref="CsvTypeMap{T,TValue}"/>.
/// </summary>
public abstract class CsvTypeMap
{
    /// <summary>
    /// Gets the <see cref="Type"/> of the mapped type.
    /// </summary>
    protected abstract Type TargetType { get; }

    /// <summary>
    /// Throws an exception for header field being bound multiple times.
    /// </summary>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}.ThrowOnDuplicate"/>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowDuplicate(
        string member,
        string field,
        ReadOnlySpan<string> headers,
        bool allowContentInExceptions)
    {
        string message = allowContentInExceptions
            ? $"'{member}' matched to multiple of the {headers.Length} headers."
            : $"\"{member}\" matched to multiple headers, including '{field}' in {JoinValues(headers)}.";

        throw new CsvBindingException(TargetType, message);
    }

    /// <summary>
    /// Throws an exception for header field that wasn't matched to any member or parameter.
    /// </summary>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}.IgnoreUnmatched"/>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowUnmatched(string field, int index, bool allowContentInExceptions)
    {
        string message = allowContentInExceptions
            ? $"Unmatched header field '{field}' at index {index}."
            : $"Unmatched header field at index {index}.";

        throw new CsvBindingException(TargetType, message);
    }

    /// <summary>
    /// Throws an exception for a required member or parameter that wasn't bound to any of the headers.
    /// </summary>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowRequiredNotRead(
        IEnumerable<string> members,
        ReadOnlySpan<string> headers,
        bool allowContentInExceptions)
    {
        string missingMembers = string.Join(", ", members.Select(x => $"\"{x}\""));
        string message = $"Required members/parameters [{missingMembers}] were not matched to any header field";

        throw new CsvBindingException(
            TargetType,
            $"{message}{(allowContentInExceptions ? $": {JoinValues(headers)}" : ".")}");
    }

    /// <summary>
    /// Throws an exception for header that couldn't be bound to any member of parameter.
    /// </summary>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}.IgnoreUnmatched"/>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowNoFieldsBound(
        ReadOnlySpan<string> headers,
        bool allowContentInExceptions)
    {
        string message = allowContentInExceptions
            ? $"No header fields were matched to a member or parameter: {JoinValues(headers)}"
            : "No header fields were matched to a member or parameter.";

        throw new CsvBindingException(TargetType, message);
    }

    private static string JoinValues(ReadOnlySpan<string> values)
    {
        // should never happen
        if (values.IsEmpty)
            return "";

        var sb = new ValueStringBuilder(stackalloc char[128]);

        sb.Append('[');

        foreach (var value in values)
        {
            sb.Append('"');
            sb.Append(value);
            sb.Append("\", ");
        }

        sb.Length -= 2;
        sb.Append(']');

        return sb.ToString();
    }
}
