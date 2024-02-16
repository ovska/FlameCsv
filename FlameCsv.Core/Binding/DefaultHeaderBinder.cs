using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
/// Binds CSV header to members.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public sealed class DefaultHeaderBinder<T> : IHeaderBinder<T> where T : unmanaged, IEquatable<T>
{
    private sealed class HeaderData
    {
        public ReadOnlyMemory<string> IgnoredValues { get; }
        public ReadOnlySpan<HeaderBindingCandidate> Candidates => _candidates.AsSpan();

        private readonly List<HeaderBindingCandidate> _candidates;

        public HeaderData(ReadOnlyMemory<string> ignoredValues, List<HeaderBindingCandidate> candidates)
        {
            IgnoredValues = ignoredValues;
            _candidates = candidates;
        }

        public void ThrowIfRequiredNotBound<TValue>(ReadOnlySpan<CsvBinding<TValue>> bindings)
        {
            List<string>? notBound = null;

            foreach (var candidate in _candidates)
            {
                if (candidate.IsRequired)
                {
                    bool bound = false;

                    foreach (var binding in bindings)
                    {
                        if (ReferenceEquals(candidate.Target, binding.Sentinel))
                        {
                            bound = true;
                            break;
                        }
                    }

                    if (!bound)
                    {
                        (notBound ??= []).Add(candidate.Value);
                    }
                }
            }

            if (notBound is { Count: > 0 })
            {
                throw new CsvBindingException<TValue>(
                    "One or more required bindings were not bound");
            }
        }
    }

    private static readonly ConditionalWeakTable<Type, HeaderData> _readCache = [];
    private static readonly ConditionalWeakTable<Type, HeaderData> _writeCache = [];

    /// <summary>
    /// Fields that could not be matched are ignored.
    /// </summary>
    public bool IgnoreUnmatched { get; }

    private readonly CsvOptions<T> _options;

    public DefaultHeaderBinder(
        CsvOptions<T> options,
        bool ignoreUnmatched = false)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        IgnoreUnmatched = ignoreUnmatched;
    }

    public CsvBindingCollection<TValue> Bind<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(ReadOnlySpan<string> headerFields)
    {
        _options.MakeReadOnly();

        HeaderData headerData = DefaultHeaderBinder<T>.GetHeaderDataFor<TValue>(write: false);
        ReadOnlySpan<string> IgnoredValues = headerData.IgnoredValues.Span;
        List<CsvBinding<TValue>> foundBindings = new(headerFields.Length);

        foreach (var field in headerFields)
        {
            int index = foundBindings.Count;

            CsvBinding<TValue>? binding = null;

            foreach (var value in IgnoredValues)
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
                throw new CsvBindingException(); // TODO
            }

            foundBindings.Add(binding ?? CsvBinding.Ignore<TValue>(index: foundBindings.Count));
        }

        return new CsvBindingCollection<TValue>(foundBindings, write: false, isInternalCall: true);
    }

    public CsvBindingCollection<TValue> Bind<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>()
    {
        _options.MakeReadOnly();

        var candidates = DefaultHeaderBinder<T>.GetHeaderDataFor<TValue>(write: true).Candidates;

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
    private static HeaderData GetHeaderDataFor<[DynamicallyAccessedMembers(Messages.ReflectionBound)] TValue>(
        bool write)
    {
        ConditionalWeakTable<Type, DefaultHeaderBinder<T>.HeaderData> cache = write ? _writeCache : _readCache;

        if (!cache.TryGetValue(typeof(TValue), out var headerData))
        {
            var typeInfo = CsvTypeInfo<TValue>.Instance;

            List<HeaderBindingCandidate> candidates = [];

            foreach (var member in typeInfo.Members)
            {
                if (!write && member.IsReadOnly)
                    continue;

                if (member.IsExcluded(write))
                    continue;

                bool found = false;

                foreach (var attribute in member.Attributes)
                {
                    if (attribute is not CsvHeaderAttribute attr ||
                        !attr.Scope.IsValidFor(write))
                    {
                        continue;
                    }

                    found = true;

                    if (attr.Values.Length == 0)
                    {
                        candidates.Add(new HeaderBindingCandidate(member.Value.Name, member.Value, attr.Order, attr.Required));
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
                    candidates.Add(new HeaderBindingCandidate(member.Value.Name, member.Value, default, isRequired: false));
                }
            }

            CsvHeaderIgnoreAttribute? ignoreAttribute = null;

            foreach (var attribute in typeInfo.Attributes)
            {
                if (attribute is CsvHeaderTargetAttribute attr && attr.Scope.IsValidFor(write))
                {
                    var member = typeInfo.GetPropertyOrField(attr.MemberName);
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

            foreach (var parameter in !write ? typeInfo.ConstructorParameters : default)
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
                        candidates.Add(new HeaderBindingCandidate(parameter.Value.Name!, parameter.Value, attr.Order, attr.Required));
                    }
                    else
                    {
                        foreach (var value in attr.Values)
                        {
                            candidates.Add(new HeaderBindingCandidate(value, parameter.Value, attr.Order, attr.Required));
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
            }

            candidates.AsSpan().Sort(); // sorted by Order

            cache.AddOrUpdate(
                typeof(TValue),
                headerData = new HeaderData(ignoreAttribute?.Values ?? default, candidates));
        }

        return headerData;
    }
}
