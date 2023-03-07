using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;

namespace FlameCsv.Binding;

/// <summary>
/// Represents a validated collection of member bindings.
/// </summary>
/// <typeparam name="TValue"></typeparam>
public sealed class CsvBindingCollection<TValue>
{
    /// <summary>
    /// All bindings, sorted by index. Guaranteed to be valid.
    /// </summary>
    public ReadOnlySpan<CsvBinding> Bindings => _allBindings.AsSpan();

    /// <summary>
    /// Member bindings, or empty if there are none.
    /// </summary>
    public ReadOnlySpan<CsvBinding> MemberBindings => _memberBindings.AsSpan();

    /// <summary>
    /// Constructor parameter bindings sorted by parameter position, or empty if there are none.
    /// </summary>
    public ReadOnlySpan<CsvBinding> ConstructorBindings => _ctorBindings.AsSpan();

    /// <summary>
    /// Return <see langword="true"/> is <see cref="ConstructorBindings"/> is not empty.
    /// </summary>
    public bool HasConstructorParameters => _ctorBindings.Count != 0;

    /// <summary>
    /// Return <see langword="true"/> is <see cref="MemberBindings"/> is not empty.
    /// </summary>
    public bool HasMemberInitializers => _memberBindings.Count != 0;

    /// <summary>
    /// Returns the constructor defined in <see cref="ConstructorBindings"/>,
    /// or the parameterless constructor if there is no constructor defined.
    /// </summary>
    public ConstructorInfo Constructor => HasConstructorParameters
        ? _ctorBindings[0].Constructor
        : ThrowHelper.ThrowInvalidOperationException<ConstructorInfo>("There is no constructor bindings.");

    private readonly List<CsvBinding> _allBindings;
    private readonly List<CsvBinding> _ctorBindings;
    private readonly List<CsvBinding> _memberBindings;

    /// <summary>
    /// Initializes a new binding collection.
    /// </summary>
    /// <param name="bindings">Column bindings</param>
    /// <exception cref="CsvBindingException">Bindings are invalid</exception>
    public CsvBindingCollection(IEnumerable<CsvBinding> bindings) : this(bindings.ToList(), true)
    {
    }

    /// <summary>
    /// Internal use only. The parameter list is modified and retained.
    /// </summary>
    internal CsvBindingCollection(List<CsvBinding> bindingsList, bool _)
    {
        Guard.IsNotEmpty(bindingsList, "bindings");

        Span<CsvBinding> bindings = bindingsList.AsSpan();
        bindings.Sort();

        int currentColumnIndex = 0;

        List<CsvBinding> memberBindings = new(bindings.Length);
        List<CsvBinding> ctorBindings = new();
        ConstructorInfo? ctor = null;

        foreach (var binding in bindings)
        {
            // Check if binding can be used to target the type
            if (!binding.IsApplicableTo<TValue>())
            {
                throw new CsvBindingException(typeof(TValue), binding);
            }

            int expectedIndex = currentColumnIndex++;

            // Indices should be gapless and start from zero
            if (binding.Index != expectedIndex)
            {
                throw new CsvBindingException(
                    $"Invalid binding indices for {typeof(TValue)}, expected {expectedIndex} "
                    + $"but the next binding was: {binding}",
                    bindingsList)
                { TargetType = typeof(TValue) };
            }

            if (binding.IsIgnored)
                continue;

            // Check that the member is unique among the bindings
            if (binding.IsMember)
            {
                ThrowIfDuplicate(memberBindings, in binding);
                memberBindings.Add(binding);
            }
            else
            {
                Debug.Assert(binding.IsParameter);

                if (ctor is null)
                {
                    ctor = binding.Constructor;
                }
                else
                {
                    ThrowIfMultipleCtors(ctor, in binding);
                }

                ThrowIfDuplicate(ctorBindings, in binding);
                ctorBindings.Add(binding);
            }
        }

        if (memberBindings.Count == 0 && ctorBindings.Count == 0)
        {
            throw new CsvBindingException(
                $"All {bindings.Length} binding(s) for {typeof(TValue)} are ignored",
                bindingsList)
            { TargetType = typeof(TValue) };
        }

        // Ensure all parameters are accounted for
        if (ctorBindings.Count != 0)
        {
            var parameters = ctor!.GetCachedParameters();

            if (ctorBindings.Count != parameters.Length)
            {
                throw new CsvBindingException(
                    $"All constructor parameters were not accounted for (got {ctorBindings.Count} out of {parameters.Length})",
                    ctorBindings);
            }

            // At this point we don't need to validate that the parameters are for the same constructor that
            // have the correct positions.
            ctorBindings.AsSpan().Sort(static (a, b) => a.Parameter.Position.CompareTo(b.Parameter.Position));
        }

        _allBindings = bindingsList;
        _memberBindings = memberBindings;
        _ctorBindings = ctorBindings;

        static void ThrowIfDuplicate(List<CsvBinding> existing, in CsvBinding binding)
        {
            foreach (ref var duplicate in existing.AsSpan())
            {
                if (duplicate.TargetEquals(binding))
                {
                    throw new CsvBindingException(typeof(TValue), duplicate, binding) { TargetType = typeof(TValue) };
                }
            }
        }

        static void ThrowIfMultipleCtors(ConstructorInfo ctor, in CsvBinding binding)
        {
            if (!ctor.Equals(binding.Constructor))
                throw new CsvBindingException(typeof(TValue), ctor, binding.Constructor) { TargetType = typeof(TValue) };
        }
    }
}
