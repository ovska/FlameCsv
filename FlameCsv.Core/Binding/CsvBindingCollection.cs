using System.Collections.Immutable;
using System.Diagnostics;
using FlameCsv.Exceptions;

namespace FlameCsv.Binding;

/// <summary>
/// Represents a validated collection of member bindings.
/// </summary>
/// <typeparam name="TValue"></typeparam>
public sealed class CsvBindingCollection<TValue>
{
    /// <summary>
    /// Collection of bindings.
    /// </summary>
    public ImmutableArray<CsvBinding> Bindings { get; }

    /// <summary>
    /// Initializes a new binding collection.
    /// </summary>
    /// <param name="bindings">Column bindings</param>
    /// <exception cref="CsvBindingException">Bindings are invalid</exception>
    public CsvBindingCollection(IEnumerable<CsvBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        var sorted = bindings.OrderBy(b => b.Index).ToImmutableArray();
        CsvBindingException.ThrowIfInvalid<TValue>(sorted);
        Bindings = sorted;
    }

    // for internal use
    internal CsvBindingCollection(ImmutableArray<CsvBinding> bindings)
    {
        Debug.Assert(!bindings.IsEmpty);
        Debug.Assert(bindings.SequenceEqual(bindings.OrderBy(x => x.Index)));
        Bindings = bindings;
    }
}
