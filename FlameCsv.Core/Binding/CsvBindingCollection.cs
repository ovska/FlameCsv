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
    /// Constructor parameters and their bindings. Parameters that don't have a binding are guaranteed
    /// to have a default value.
    /// </summary>
    public ReadOnlySpan<(CsvBinding? binding, ParameterInfo param)> ConstructorParameters => _ctorParameters.AsSpan();

    /// <summary>
    /// Return <see langword="true"/> is <see cref="ConstructorBindings"/> is not empty.
    /// </summary>
    public bool HasConstructorParameters => _ctorParameters?.Count > 0;

    /// <summary>
    /// Return <see langword="true"/> is <see cref="MemberBindings"/> is not empty.
    /// </summary>
    public bool HasMemberInitializers => _memberBindings.Count > 0;

    /// <summary>
    /// Returns the constructor defined in <see cref="ConstructorBindings"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public ConstructorInfo Constructor => _ctor ??
        ThrowHelper.ThrowInvalidOperationException<ConstructorInfo>("There is no constructor bindings.");

    private readonly List<CsvBinding> _allBindings;
    private readonly List<CsvBinding> _memberBindings;
    private readonly List<(CsvBinding? binding, ParameterInfo param)>? _ctorParameters;
    private readonly ConstructorInfo? _ctor;

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
    internal CsvBindingCollection(List<CsvBinding> bindingsList, bool isInternalCall)
    {
        Debug.Assert(isInternalCall);
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

        _allBindings = bindingsList;
        _memberBindings = memberBindings;
        _ctor = ctor;
        _ctorParameters = GetConstructorParameters(ctorBindings, ctor);

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

    private static List<(CsvBinding? binding, ParameterInfo param)>? GetConstructorParameters(
        List<CsvBinding> bindingsList,
        ConstructorInfo? ctor)
    {
        if (bindingsList.Count == 0)
            return null;

        var bindings = bindingsList.AsSpan();
        var parameters = ctor!.GetCachedParameters();

        // Guard against some weirdness possible by custom header binders
        if (bindings.Length > parameters.Length)
            throw new CsvBindingException(
                $"Invalid constructor bindings, got {bindings.Length} but ctor had {parameters.Length} parameters.");

        List<(CsvBinding? ctorBinding, ParameterInfo param)> parameterInfos = new(parameters.Length);

        foreach (var parameter in parameters)
        {
            CsvBinding? match = null;

            foreach (var binding in bindings)
            {
                if (binding.Parameter.Equals(parameter))
                {
                    match = binding;
                    break;
                }
            }

            // Default value is required
            if (!match.HasValue && !parameter.HasDefaultValue)
            {
                throw new CsvBindingException(typeof(TValue), parameter);
            }

            parameterInfos.Add((match, parameter));
        }

        // At this point we don't need to validate that the parameters are for the same constructor that
        // have the correct positions.
        parameterInfos.AsSpan().Sort(static (a, b) => a.param.Position.CompareTo(b.param.Position));
        return parameterInfos;
    }
}
