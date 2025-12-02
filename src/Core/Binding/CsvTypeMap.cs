using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;
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
    /// Throws an exception for header field being bound multiple times.
    /// </summary>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowDuplicate(
        string member,
        string field,
        ImmutableArray<string> headers,
        IEqualityComparer<string> comparer
    )
    {
        throw new CsvBindingException(
            TargetType,
            $"\"{member}\" matched to multiple headers, including '{field}' using {comparer} in {JoinValues(headers)}."
        );
    }

    /// <summary>
    /// Throws an exception for header field that wasn't matched to any member or parameter.
    /// </summary>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowUnmatched(string field, int index, ImmutableArray<string> headers)
    {
        throw new CsvBindingException(
            TargetType,
            $"Unmatched header field '{field}' at index {index}: [{JoinValues(headers)}]"
        );
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
            $"Required members/parameters [{missingMembers}] were not matched to any header field: [{JoinValues(headers)}]"
        );
    }

    /// <summary>
    /// Throws an exception for header that couldn't be bound to any member of parameter.
    /// </summary>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowNoFieldsBound(ImmutableArray<string> headers)
    {
        throw new CsvBindingException(
            TargetType,
            $"No header fields were matched to a member or parameter: {JoinValues(headers)}"
        );
    }

    private static string JoinValues(ImmutableArray<string> values) =>
        values.IsDefaultOrEmpty ? "" : string.Join(", ", values.Select(x => $"\"{x}\""));
}
