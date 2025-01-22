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
    private static readonly TrimmingCache<object, object> _materializers = new();
    private static readonly TrimmingCache<object, object> _dematerializers = new();

    public static IMaterializer<T, TValue> GetMaterializer<T, TValue>(
        CsvTypeMap<T, TValue> map,
        scoped ReadOnlySpan<string> headers,
        CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        // TODO: use headers to cache

        // if (_materializers.TryGetValue(map, out object? cached))
        //     return (IMaterializer<T, TValue>)cached;

        var materializer = map.BindMembers(headers, options);
        // _materializers.Add(map, materializer);
        return materializer;
    }

    public static IMaterializer<T, TValue> GetMaterializer<T, TValue>(
        CsvTypeMap<T, TValue> map,
        CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (_materializers.TryGetValue(map, out object? cached))
            return (IMaterializer<T, TValue>)cached;

        var materializer = map.BindMembers(options);
        _materializers.Add(map, materializer);
        return materializer;
    }

    public static IDematerializer<T, TValue> GetDematerializer<T, TValue>(
        CsvTypeMap<T, TValue> map,
        CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (_dematerializers.TryGetValue(map, out object? cached))
            return (IDematerializer<T, TValue>)cached;

        var dematerializer = map.GetDematerializer(options);
        _dematerializers.Add(map, dematerializer);
        return dematerializer;
    }


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
        ReadOnlySpan<string> headers)
    {
        throw new CsvBindingException(
            TargetType,
            $"\"{member}\" matched to multiple headers, including '{field}' in {JoinValues(headers)}.");
    }

    /// <summary>
    /// Throws an exception for header field that wasn't matched to any member or parameter.
    /// </summary>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}.IgnoreUnmatched"/>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowUnmatched(string field, int index)
    {
        throw new CsvBindingException(TargetType, $"Unmatched header field '{field}' at index {index}.");
    }

    /// <summary>
    /// Throws an exception for a required member or parameter that wasn't bound to any of the headers.
    /// </summary>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowRequiredNotRead(IEnumerable<string> members, ReadOnlySpan<string> headers)
    {
        string missingMembers = string.Join(", ", members.Select(x => $"\"{x}\""));
        throw new CsvBindingException(
            TargetType,
            $"Required members/parameters [{missingMembers}] were not matched to any header field: [{JoinValues(headers)}]");
    }

    /// <summary>
    /// Throws an exception for header that couldn't be bound to any member of parameter.
    /// </summary>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}.IgnoreUnmatched"/>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowNoFieldsBound(ReadOnlySpan<string> headers)
    {
        throw new CsvBindingException(
            TargetType,
            $"No header fields were matched to a member or parameter: {JoinValues(headers)}");
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
