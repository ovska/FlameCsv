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
    public ReadOnlySpan<CsvBinding> Bindings => _bindingsSorted.AsSpan();

    /// <summary>
    /// Member bindings sorted by index, or empty if there are none.
    /// </summary>
    public ReadOnlySpan<CsvBinding> MemberBindings => _memberBindings.AsSpan();

    /// <summary>
    /// Constructor parameter bindings sorted by index, or empty if there are none.
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
        : ReflectionCache<TValue>.ParameterlessCtor;

    internal readonly List<CsvBinding> _bindingsSorted;
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
        int nonIgnoredCount = 0;

        List<CsvBinding> members = new(bindings.Length);
        List<CsvBinding> ctorParameters = new();
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

            nonIgnoredCount++;

            // Check that the member is unique among the bindings
            if (binding.IsMember)
            {
                ThrowIfDuplicate(members, in binding);
                members.Add(binding);
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

                ThrowIfDuplicate(ctorParameters, in binding);
                ctorParameters.Add(binding);
            }
        }

        if (nonIgnoredCount == 0)
        {
            throw new CsvBindingException($"All bindings for {typeof(TValue)} are ignored", bindingsList)
            {
                TargetType = typeof(TValue),
            };
        }

        // Ensure all parameters are accounted for
        if (ctorParameters.Count != 0)
        {

        }
        ctorParameters.AsSpan().Sort(static (a, b) => a.Parameter.Position.CompareTo(b.Parameter.Position));


        //
        _bindingsSorted = bindingsList;
        _memberBindings = members;
        _ctorBindings = ctorParameters;

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
