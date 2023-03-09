using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;

namespace FlameCsv.Binding;

/// <summary>
/// Represents a validated collection of CSV columns bound to properties, fields, or constructor parameters.
/// </summary>
/// <typeparam name="TValue">Type represented by the CSV</typeparam>
public sealed class CsvBindingCollection<TValue>
{
    /// <summary>
    /// All bindings, sorted by index. Guaranteed to be valid.
    /// </summary>
    internal ReadOnlySpan<CsvBinding<TValue>> Bindings => _allBindings.AsSpan();

    /// <summary>
    /// Member bindings, or empty if there are none.
    /// </summary>
    internal ReadOnlySpan<MemberCsvBinding<TValue>> MemberBindings => _memberBindings.AsSpan();

    /// <summary>
    /// Constructor parameters and their bindings. Parameters that don't have a binding are guaranteed
    /// to have a default value.
    /// </summary>
    internal ReadOnlySpan<(ParameterCsvBinding<TValue>? binding, ParameterInfo param)> ConstructorParameters
        => _ctorParameters.AsSpan();

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
        ThrowHelper.ThrowInvalidOperationException<ConstructorInfo>("There are no constructor bindings.");

    private readonly List<CsvBinding<TValue>> _allBindings;
    private readonly List<MemberCsvBinding<TValue>> _memberBindings;
    private readonly List<(ParameterCsvBinding<TValue>? binding, ParameterInfo param)>? _ctorParameters;
    private readonly ConstructorInfo? _ctor;

    /// <summary>
    /// Initializes a new binding collection.
    /// </summary>
    /// <param name="bindings">Column bindings</param>
    /// <exception cref="CsvBindingException">Bindings are invalid</exception>
    public CsvBindingCollection(IEnumerable<CsvBinding<TValue>> bindings) : this(bindings.ToList(), true)
    {
    }

    /// <summary>
    /// Internal use only. The parameter list is modified and retained.
    /// </summary>
    internal CsvBindingCollection(List<CsvBinding<TValue>> bindingsList, bool isInternalCall)
    {
        Debug.Assert(isInternalCall);
        Guard.IsNotEmpty(bindingsList, "bindings");

        Span<CsvBinding<TValue>> bindings = bindingsList.AsSpan();
        bindings.Sort();

        int currentColumnIndex = 0;

        List<MemberCsvBinding<TValue>> memberBindings = new(bindings.Length);
        List<ParameterCsvBinding<TValue>> ctorBindings = new();
        ConstructorInfo? ctor = null;

        foreach (var binding in bindings)
        {
            int expectedIndex = currentColumnIndex++;

            // Indices should be gapless and start from zero
            if (binding.Index != expectedIndex)
            {
                throw new CsvBindingException<TValue>(
                    $"Invalid binding indices for {typeof(TValue)}, expected {expectedIndex} "
                    + $"but the next binding was: {binding}",
                    bindingsList);
            }

            if (binding.IsIgnored)
                continue;

            // Check that the member is unique among the bindings
            if (binding is MemberCsvBinding<TValue> memberBinding)
            {
                ThrowIfDuplicate(memberBindings, memberBinding);
                memberBindings.Add(memberBinding);
            }
            else if (binding is ParameterCsvBinding<TValue> parameterBinding)
            {
                if (ctor is null)
                {
                    ctor = parameterBinding.Constructor;
                }
                else
                {
                    ThrowIfMultipleCtors(ctor, parameterBinding);
                }

                ThrowIfDuplicate(ctorBindings, parameterBinding);
                ctorBindings.Add(parameterBinding);
            }
            else
            {
                throw new UnreachableException("Invalid binding type");
            }
        }

        if (memberBindings.Count == 0 && ctorBindings.Count == 0)
        {
            throw new CsvBindingException<TValue>(
                $"All {bindings.Length} binding(s) for {typeof(TValue)} are ignored",
                bindingsList);
        }

        _allBindings = bindingsList;
        _memberBindings = memberBindings;
        _ctor = ctor;
        _ctorParameters = GetConstructorParameters(ctorBindings, ctor!);

        static void ThrowIfDuplicate<TBinding>(List<TBinding> existing, TBinding binding)
            where TBinding : CsvBinding<TValue>
        {
            foreach (var duplicate in existing.AsSpan())
            {
                if (duplicate.TargetEquals(binding))
                {
                    throw new CsvBindingException<TValue>(duplicate, binding);
                }
            }
        }

        static void ThrowIfMultipleCtors(ConstructorInfo ctor, ParameterCsvBinding<TValue> binding)
        {
            if (!ctor.Equals(binding.Constructor))
                throw new CsvBindingException<TValue>(typeof(TValue), ctor, binding.Constructor);
        }
    }

    private static List<(ParameterCsvBinding<TValue>? binding, ParameterInfo param)>? GetConstructorParameters(
        List<ParameterCsvBinding<TValue>> bindingsList,
        ConstructorInfo ctor)
    {
        if (bindingsList.Count == 0)
            return null;

        var bindings = bindingsList.AsSpan();
        ReadOnlySpan<ParameterInfo> parameters = ctor!.GetParameters();

        // Guard against some weirdness possible by custom header binders
        if (bindings.Length > parameters.Length)
            throw new CsvBindingException<TValue>(
                $"Invalid constructor bindings, got {bindings.Length} but ctor had {parameters.Length} parameters.");

        List<(ParameterCsvBinding<TValue>? ctorBinding, ParameterInfo param)> parameterInfos = new(parameters.Length);

        foreach (var parameter in parameters)
        {
            ParameterCsvBinding<TValue>? match = null;

            foreach (var binding in bindings)
            {
                if (binding.Parameter.Equals(parameter))
                {
                    match = binding;
                    break;
                }
            }

            // Default value is required
            if (match is null && !parameter.HasDefaultValue)
            {
                throw new CsvBindingException<TValue>(parameter);
            }

            parameterInfos.Add((match, parameter));
        }

        // At this point we don't need to validate that the parameters are for the same constructor that
        // have the correct positions.
        parameterInfos.AsSpan().Sort(static (a, b) => a.param.Position.CompareTo(b.param.Position));
        return parameterInfos;
    }
}
