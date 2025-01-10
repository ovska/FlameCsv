using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

/// <summary>
/// Internal implementation detail.
/// </summary>
public abstract class DefaultHeaderBinder
{
    internal static readonly ConditionalWeakTable<Type, HeaderData> ReadCache = [];
    internal static readonly ConditionalWeakTable<Type, HeaderData> WriteCache = [];

    internal sealed class HeaderData(string[]? ignoredValues, List<HeaderBindingCandidate> candidates)
    {
        public ReadOnlySpan<string> IgnoredValues => ignoredValues;
        public ReadOnlySpan<HeaderBindingCandidate> Candidates => candidates.AsSpan();
    }
}

/// <summary>
/// Binds CSV header to members.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public sealed class DefaultHeaderBinder<T> : DefaultHeaderBinder, IHeaderBinder<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Fields that could not be matched are ignored.
    /// </summary>
    public bool IgnoreUnmatched { get; }

    private readonly CsvOptions<T> _options;

    /// <summary>
    /// Creates a new header binder.
    /// </summary>
    public DefaultHeaderBinder(
        CsvOptions<T> options,
        bool ignoreUnmatched = false)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        IgnoreUnmatched = ignoreUnmatched;
    }

    /// <inheritdoc />
    public CsvBindingCollection<TValue> Bind<[DAM(Messages.ReflectionBound)] TValue>(
        ReadOnlySpan<string> headerFields)
    {
        _options.MakeReadOnly();

        HeaderData headerData = GetHeaderDataFor<TValue>(write: false);
        ReadOnlySpan<string> ignoredValues = headerData.IgnoredValues;
        List<CsvBinding<TValue>> foundBindings = new(headerFields.Length);

        foreach (var field in headerFields)
        {
            int index = foundBindings.Count;

            CsvBinding<TValue>? binding = null;

            foreach (var value in ignoredValues)
            {
                if (_options.Comparer.Equals(value, field))
                {
                    binding = CsvBinding.Ignore<TValue>(index);
                    break;
                }
            }

            if (binding is null)
            {
                foreach (ref readonly var candidate in headerData.Candidates)
                {
                    if (_options.Comparer.Equals(candidate.Value, field))
                    {
                        binding = CsvBinding.FromHeaderBinding<TValue>(index, in candidate);
                        break;
                    }
                }
            }

            if (binding is null && !IgnoreUnmatched)
            {
                throw new CsvBindingException(
                    $"Could not bind header '{field}' at index {index} to type {typeof(TValue).FullName}");
            }

            foundBindings.Add(binding ?? CsvBinding.Ignore<TValue>(index: foundBindings.Count));
        }

        return new CsvBindingCollection<TValue>(foundBindings, write: false, isInternalCall: true);
    }

    /// <inheritdoc />
    public CsvBindingCollection<TValue> Bind<[DAM(Messages.ReflectionBound)] TValue>()
    {
        _options.MakeReadOnly();

        var candidates = GetHeaderDataFor<TValue>(write: true).Candidates;

        List<CsvBinding<TValue>> result = new(candidates.Length);
        HashSet<object> handledMembers = [];
        int index = 0;

        foreach (var candidate in candidates)
        {
            Debug.Assert(candidate.Target is not ParameterInfo);

            if (handledMembers.Add(candidate.Target))
                result.Add(CsvBinding.FromHeaderBinding<TValue>(index++, in candidate));
        }

        return new CsvBindingCollection<TValue>(result, write: true, isInternalCall: true);
    }

    /// <summary>
    /// Returns members of <typeparamref name="TValue"/> that can be used for binding.
    /// </summary>
    /// <seealso cref="CsvHeaderAttribute"/>
    /// <seealso cref="CsvHeaderExcludeAttribute"/>
    private static HeaderData GetHeaderDataFor<[DAM(Messages.ReflectionBound)] TValue>(
        bool write)
    {
        ConditionalWeakTable<Type, HeaderData> cache = write ? WriteCache : ReadCache;

        if (!cache.TryGetValue(typeof(TValue), out var headerData))
        {
            List<HeaderBindingCandidate> candidates = [];

            foreach (var member in CsvTypeInfo.Members<TValue>())
            {
                if (!write && member.IsReadOnly)
                    continue;

                if (member.IsExcluded(write))
                    continue;

                bool found = false;

                foreach (var attribute in member.Attributes)
                {
                    if (attribute is not CsvHeaderAttribute attr || !attr.Scope.IsValidFor(write))
                    {
                        continue;
                    }

                    found = true;

                    if (attr.Values.Length == 0)
                    {
                        candidates.Add(
                            new HeaderBindingCandidate(member.Value.Name, member.Value, attr.Order, attr.Required));
                    }
                    else
                    {
                        foreach (var value in attr.Values)
                        {
                            candidates.Add(new HeaderBindingCandidate(value, member.Value, attr.Order, attr.Required));
                        }
                    }
                }

                if (!found)
                {
                    candidates.Add(
                        new HeaderBindingCandidate(member.Value.Name, member.Value, default, isRequired: false));
                }
            }

            CsvHeaderIgnoreAttribute? ignoreAttribute = null;

            foreach (var attribute in CsvTypeInfo.Attributes<TValue>())
            {
                if (attribute is CsvHeaderTargetAttribute attr && attr.Scope.IsValidFor(write))
                {
                    var member = CsvTypeInfo.GetPropertyOrField<TValue>(attr.MemberName);
                    candidates.EnsureCapacity(candidates.Count + attr.Values.Length);

                    foreach (var value in attr.Values)
                    {
                        candidates.Add(new HeaderBindingCandidate(value, member.Value, attr.Order, attr.IsRequired));
                    }
                }
                else if (ignoreAttribute is null
                         && attribute is CsvHeaderIgnoreAttribute hia
                         && hia.Scope.IsValidFor(write))
                {
                    ignoreAttribute = hia;
                }
            }

            foreach (var parameter in !write ? CsvTypeInfo.ConstructorParameters<TValue>() : default)
            {
                CsvHeaderAttribute? attr = null;

                foreach (var attribute in parameter.Attributes)
                {
                    if (attribute is CsvHeaderAttribute match && match.Scope.IsValidFor(write))
                    {
                        attr = match;
                        break;
                    }
                }

                if (attr is not null)
                {
                    if (attr.Values.Length == 0)
                    {
                        candidates.Add(
                            new HeaderBindingCandidate(
                                parameter.Value.Name!,
                                parameter.Value,
                                attr.Order,
                                attr.Required));
                    }
                    else
                    {
                        foreach (var value in attr.Values)
                        {
                            candidates.Add(
                                new HeaderBindingCandidate(value, parameter.Value, attr.Order, attr.Required));
                        }
                    }
                }
                else
                {
                    candidates.Add(
                        new HeaderBindingCandidate(
                            parameter.Value.Name!,
                            parameter.Value,
                            default,
                            isRequired: false));
                }

                // hack for records: remove init-only properties with the same name and type
                // as a parameter
                for (int i = candidates.Count - 1; i >= 0; i--)
                {
                    HeaderBindingCandidate existing = candidates[i];

                    if (existing.Target is PropertyInfo prop
                        && prop.Name == parameter.Value.Name
                        && prop.PropertyType == parameter.Value.ParameterType
                        && prop.SetMethod is { ReturnParameter: var rp }
                        && rp.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                    {
                        candidates.RemoveAt(i);
                    }
                }
            }

            candidates.AsSpan().Sort(); // sorted by Order

            cache.AddOrUpdate(
                typeof(TValue),
                headerData = new HeaderData(ignoreAttribute?.Values, candidates));
        }

        return headerData;
    }
}
