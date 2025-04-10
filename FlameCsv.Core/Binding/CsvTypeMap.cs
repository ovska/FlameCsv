using System.Collections.Immutable;
using System.ComponentModel;
using FlameCsv.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Attributes;
using JetBrains.Annotations;

namespace FlameCsv.Binding;

/// <summary>
/// Base class providing throw helpers for <see cref="CsvTypeMap{T,TValue}"/>.
/// </summary>
[PublicAPI]
public abstract class CsvTypeMap
{
    /// <summary>
    /// Returns the mapped type.
    /// </summary>
    protected abstract Type TargetType { get; }

    /// <summary>
    /// If <see langword="true"/>, headers that cannot be matched to a member are ignored instead of throwing.
    /// </summary>
    public bool IgnoreUnmatched { get; init; }

    /// <summary>
    /// If <see langword="true"/>, multiple header field matches to a single member throw an exception.
    /// The default behavior does not attempt to match already matched members.
    /// </summary>
    public bool ThrowOnDuplicate { get; init; }

    /// <summary>
    /// Disables caching of de/materializers for this type map.
    /// </summary>
    /// <remarks>
    /// Caching is enabled by default.
    /// The cache is used on calls to public methods on the base class <see cref="CsvTypeMap"/>.
    /// </remarks>
    /// <seealso cref="CsvTypeMap{T,TValue}.GetMaterializer(System.Collections.Immutable.ImmutableArray{string},CsvOptions{T})"/>
    /// <seealso cref="CsvTypeMap{T,TValue}.GetMaterializer(FlameCsv.CsvOptions{T})"/>
    /// <seealso cref="CsvTypeMap{T,TValue}.GetDematerializer"/>
    public bool NoCaching { get; init; }

    /// <summary>
    /// Throws an exception for header field being bound multiple times.
    /// </summary>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}.ThrowOnDuplicate"/>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowDuplicate(
        string member,
        string field,
        ImmutableArray<string> headers)
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowUnmatched(string field, int index)
    {
        throw new CsvBindingException(TargetType, $"Unmatched header field '{field}' at index {index}.");
    }

    /// <summary>
    /// Throws an exception for a required member or parameter that wasn't bound to any of the headers.
    /// </summary>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowRequiredNotRead(IEnumerable<string> members, ImmutableArray<string> headers)
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowNoFieldsBound(ImmutableArray<string> headers)
    {
        throw new CsvBindingException(
            TargetType,
            $"No header fields were matched to a member or parameter: {JoinValues(headers)}");
    }

    private static string JoinValues(ImmutableArray<string> values)
        => values.IsDefaultOrEmpty ? "" : string.Join(", ", values.Select(x => $"\"{x}\""));
}
