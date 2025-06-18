using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CommunityToolkit.HighPerformance;
using FlameCsv.Exceptions;
using JetBrains.Annotations;

namespace FlameCsv.Binding;

/// <summary>
/// Represents a validated collection of CSV fields bound to properties, fields, or constructor parameters.
/// </summary>
/// <typeparam name="TValue">Type represented by the CSV</typeparam>
[PublicAPI]
[DebuggerDisplay("CsvBindingCollection<{typeof(TValue).Name,nq}>[{_allBindings.Count}]")]
public sealed class CsvBindingCollection<TValue> : IEnumerable<CsvBinding<TValue>>
{
    /// <summary>
    /// Returns <c>true</c> if the bindings are for writing CSV.
    /// </summary>
    public bool ForWriting { get; }

    /// <summary>
    /// Returns <c>true</c> if the bindings are for reading CSV.
    /// </summary>
    public bool ForReading => !ForWriting;

    /// <summary>
    /// All bindings, sorted by index. Guaranteed to be valid.
    /// </summary>
    public ReadOnlySpan<CsvBinding<TValue>> Bindings => _allBindings.AsSpan();

    /// <summary>
    /// Member bindings, or empty if there are none.
    /// </summary>
    internal ReadOnlySpan<MemberCsvBinding<TValue>> MemberBindings => _memberBindings.AsSpan();

    /// <summary>
    /// Constructor parameters and their bindings. Parameters that don't have a binding are guaranteed
    /// to have a default value.
    /// </summary>
    internal ReadOnlySpan<(ParameterCsvBinding<TValue>? binding, ParameterInfo param)> ConstructorParameters =>
        _ctorParameters.AsSpan();

    /// <summary>
    /// Returns <c>true</c> if the bindings will use a specific constructor.
    /// </summary>
    /// <remarks>This is always <c>false</c> if <see cref="ForWriting"/> is <c>true</c>.</remarks>
    public bool HasConstructorParameters => _ctorParameters?.Count > 0;

    /// <summary>
    /// Return <c>true</c> is <see cref="MemberBindings"/> is not empty.
    /// </summary>
    /// <remarks>This is always <c>true</c> if <see cref="ForWriting"/> is <c>true</c>.</remarks>
    public bool HasMemberInitializers => _memberBindings.Count > 0;

    /// <summary>
    /// Returns the constructor used by the bindings.
    /// </summary>
    /// <seealso cref="HasConstructorParameters"/>
    /// <exception cref="InvalidOperationException">There are no constructor bindings</exception>
    public ConstructorInfo Constructor =>
        _ctor ?? throw new InvalidOperationException("There are no constructor bindings.");

    private readonly List<CsvBinding<TValue>> _allBindings;
    private readonly List<MemberCsvBinding<TValue>> _memberBindings;
    private readonly List<(ParameterCsvBinding<TValue>? binding, ParameterInfo param)>? _ctorParameters;
    private readonly ConstructorInfo? _ctor;

    /// <summary>
    /// Initializes a new binding collection.
    /// </summary>
    /// <param name="typeBindings">Bindings to initialize the collection with</param>
    /// <param name="write">The bindings are for writing and not reading CSV</param>
    /// <param name="ignoreDuplicates">
    /// Whether bindings targeting the same member/parameter should be ignored instead of throwing an exception
    /// </param>
    /// <exception cref="CsvBindingException">Bindings are invalid</exception>
    public CsvBindingCollection(IEnumerable<CsvBinding<TValue>> typeBindings, bool write, bool ignoreDuplicates)
    {
        ArgumentNullException.ThrowIfNull(typeBindings);

        List<CsvBinding<TValue>> bindingsList = [.. typeBindings];

        if (bindingsList.Count == 0)
        {
            throw new ArgumentException("No bindings provided", nameof(typeBindings));
        }

        Span<CsvBinding<TValue>> bindings = bindingsList.AsSpan();
        bindings.Sort(); // sort by index

        int index = 0;

        List<MemberCsvBinding<TValue>> memberBindings = new(bindings.Length);
        List<ParameterCsvBinding<TValue>> ctorBindings = [];
        ConstructorInfo? ctor = null;

        for (int i = 0; i < bindings.Length; i++)
        {
            CsvBinding<TValue> binding = bindings[i];
            int expectedIndex = index++;

            // Indices should be gapless and start from zero
            if (binding.Index != expectedIndex)
            {
                throw new CsvBindingException(
                    $"Invalid binding indices for {typeof(TValue)}, expected {expectedIndex} "
                        + $"but the next binding was: {binding}",
                    bindingsList
                )
                {
                    TargetType = typeof(TValue),
                };
            }

            if (binding.IsIgnored)
            {
                continue;
            }

            if (IsDuplicate(bindings.Slice(0, i), binding, out int otherIndex))
            {
                if (!ignoreDuplicates)
                {
                    throw new CsvBindingException(first: binding, second: bindings[otherIndex])
                    {
                        TargetType = typeof(TValue),
                    };
                }

                // remove the binding from the collection if already added
                _ = bindings[otherIndex] switch
                {
                    MemberCsvBinding<TValue> mb => memberBindings.Remove(mb),
                    ParameterCsvBinding<TValue> pb => ctorBindings.Remove(pb),
                    _ => false,
                };

                // replace the first binding with an ignored one if duplicates are allowed so it doesn't match again
                bindings[otherIndex] = CsvBinding.Ignore<TValue>(expectedIndex);
            }

            // Check that the member is unique among the bindings
            if (binding is MemberCsvBinding<TValue> memberBinding)
            {
                memberBindings.Add(memberBinding);
            }
            else if (!write && binding is ParameterCsvBinding<TValue> parameterBinding)
            {
                if (ctor is null)
                {
                    ctor = parameterBinding.Constructor;
                }
                else if (!ctor.Equals(parameterBinding.Constructor))
                {
                    throw new CsvBindingException(typeof(TValue), ctor, parameterBinding.Constructor);
                }

                ctorBindings.Add(parameterBinding);
            }
            else
            {
                throw new CsvBindingException($"Unrecognized binding type {binding.GetType().FullName}");
            }
        }

        if (memberBindings.Count == 0 && ctorBindings.Count == 0)
        {
            throw new CsvBindingException(
                $"All {bindings.Length} binding(s) for {typeof(TValue)} are ignored",
                bindingsList
            )
            {
                TargetType = typeof(TValue),
            };
        }

        ForWriting = write;
        _allBindings = bindingsList;
        _memberBindings = memberBindings;
        _ctor = ctor;
        _ctorParameters = GetConstructorParameters(ctorBindings, ctor);

        static bool IsDuplicate(ReadOnlySpan<CsvBinding<TValue>> existing, CsvBinding<TValue> binding, out int index)
        {
            for (int i = 0; i < existing.Length; i++)
            {
                var duplicate = existing[i];

                if (!ReferenceEquals(duplicate, binding) && duplicate.TargetEquals(binding))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }
    }

    private static List<(ParameterCsvBinding<TValue>? binding, ParameterInfo param)>? GetConstructorParameters(
        List<ParameterCsvBinding<TValue>> bindingsList,
        ConstructorInfo? ctor
    )
    {
        if (ctor is null || bindingsList.Count == 0)
            return null;

        var bindings = bindingsList.AsSpan();
        ReadOnlySpan<ParameterInfo> parameters = ctor.GetParameters();

        // Guard against some weirdness possible by custom header binders
        if (bindings.Length > parameters.Length)
        {
            throw new CsvBindingException(
                $"Invalid constructor bindings, got {bindings.Length} but ctor had {parameters.Length} parameters."
            )
            {
                TargetType = typeof(TValue),
            };
        }

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
                throw new CsvBindingException(parameter, bindingsList) { TargetType = typeof(TValue) };
            }

            parameterInfos.Add((match, parameter));
        }

        Debug.Assert(parameterInfos.DistinctBy(p => p.param.Member).Count() == 1);
        parameterInfos.AsSpan().Sort(static (a, b) => a.param.Position.CompareTo(b.param.Position));
        return parameterInfos;
    }

    IEnumerator<CsvBinding<TValue>> IEnumerable<CsvBinding<TValue>>.GetEnumerator()
    {
        return _allBindings.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<CsvBinding<TValue>>)this).GetEnumerator();
}
