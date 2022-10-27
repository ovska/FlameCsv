using System.Collections.Immutable;
using System.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;

namespace FlameCsv.Exceptions;

/// <summary>
/// Represents errors in CSV member binding configuration, such as invalid member types or columns.
/// </summary>
public sealed class CsvBindingException : CsvConfigurationException
{
    private sealed class CsvBindingMemberComparer : IEqualityComparer<CsvBinding>
    {
        public static readonly CsvBindingMemberComparer Instance = new();

        public bool Equals(CsvBinding x, CsvBinding y) => x.Member.Equals(y.Member);
        public int GetHashCode(CsvBinding obj) => obj.Member.GetHashCode();
    }

    /// <summary>
    /// Throws an exception if the bindings are empty, cannot be applied for <typeparamref name="TValue"/>,
    /// have missing or invalid indexes, or duplicate target members.
    /// </summary>
    /// <exception cref="CsvBindingException"/>
    public static void ThrowIfInvalid<TValue>(IEnumerable<CsvBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var bindingsList = bindings.ToList();

        if (bindingsList.Count == 0)
        {
            throw new CsvBindingException($"The binding collection for {typeof(TValue)} is empty", bindingsList)
            {
                TargetType = typeof(TValue),
            };
        }

        InternalThrowIfInvalid<TValue>(bindingsList);
    }

    internal static void InternalThrowIfInvalid<TValue>(List<CsvBinding> bindings)
    {
        Debug.Assert(bindings.Count > 0);

        bindings.AsSpan().Sort(static (a, b) => a.Index.CompareTo(b.Index));

        int currentColumnIndex = 0;
        int nonIgnoredCount = 0;

        var members = new HashSet<CsvBinding>(bindings.Count, CsvBindingMemberComparer.Instance);

        foreach (var binding in bindings)
        {
            // Check if binding can be used to target the type
            if (!binding.IsApplicableTo<TValue>())
            {
                throw new CsvBindingException(typeof(TValue), binding);
            }

            var expectedIndex = currentColumnIndex++;

            // Indices should be gapless and start from zero
            if (binding.Index != expectedIndex)
            {
                throw new CsvBindingException(
                    $"Invalid binding indices for {typeof(TValue)}, expected {expectedIndex} "
                    + $"but the next binding was: {binding}",
                    bindings) { TargetType = typeof(TValue) };
            }

            // Check that the member is unique among the bindings
            if (!binding.IsIgnored)
            {
                if (members.TryGetValue(binding, out var duplicate))
                {
                    throw new CsvBindingException(typeof(TValue), duplicate, binding) { TargetType = typeof(TValue) };
                }

                members.Add(binding);
                nonIgnoredCount++;
            }
        }

        if (nonIgnoredCount == 0)
        {
            throw new CsvBindingException($"All bindings for {typeof(TValue)} are ignored", bindings)
            {
                TargetType = typeof(TValue),
            };
        }
    }

    /// <summary>
    /// Target type of the attempted binding.
    /// </summary>
    public Type? TargetType { get; init; }

    /// <summary>
    /// Possible bindings that caused the exception.
    /// </summary>
    public IReadOnlyList<CsvBinding>? Bindings { get; }

    public CsvBindingException(
        string? message = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
    }

    public CsvBindingException(
        string message,
        IEnumerable<CsvBinding> bindings)
        : base(message)
    {
        Bindings = bindings.ToList();
    }

    /// <summary>
    /// Throws an exception for invalid target for a binding.
    /// </summary>
    /// <param name="target">Target type</param>
    /// <param name="binding">Binding not applicable for the target</param>
    public CsvBindingException(Type target, CsvBinding binding)
        : base($"{binding} cannot be used for type {target}")
    {
        Bindings = new[] { binding };
        TargetType = target;
    }

    /// <summary>
    /// Throws an exception for conflicting bindings.
    /// </summary>
    public CsvBindingException(
        Type target,
        CsvBinding first,
        CsvBinding second)
        : base($"Conflicting bindings for type {target}: {first} and {second}")
    {
        Bindings = new[] { first, second };
        TargetType = target;
    }

    /// <summary>
    /// Throws an exception for multiple overrides.
    /// </summary>
    public CsvBindingException(
        Type target,
        CsvBinding binding,
        ICsvParserOverride first,
        ICsvParserOverride second)
        : base($"Multiple parser overrides defined for {binding}: {first.GetType()} and {second.GetType()}")
    {
        Bindings = new[] { binding };
        TargetType = target;
    }
}
