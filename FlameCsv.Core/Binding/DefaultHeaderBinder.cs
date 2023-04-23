using System.Buffers;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding.Attributes;
using FlameCsv.Configuration;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

/// <summary>
/// Binds CSV header to members.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public sealed class DefaultHeaderBinder<T> : IHeaderBinder<T>
    where T : unmanaged, IEquatable<T>
{
    private sealed class HeaderData
    {
        public SpanPredicate<T>? Ignore { get; }
        public ReadOnlySpan<HeaderBindingCandidate> Candidates => _candidates.AsSpan();

        private readonly List<HeaderBindingCandidate> _candidates;

        public HeaderData(SpanPredicate<T>? ignore, List<HeaderBindingCandidate> candidates)
        {
            Ignore = ignore;
            _candidates = candidates;
        }
    }

    private static readonly ConditionalWeakTable<Type, HeaderData> _candidateCache = new();

    /// <summary>
    /// Columns that could not be matched are ignored.
    /// </summary>
    public bool IgnoreUnmatched { get; }

    public CsvReaderOptions<T> Options { get; }

    public DefaultHeaderBinder(
        CsvReaderOptions<T> options,
        bool ignoreUnmatched = false)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
        IgnoreUnmatched = ignoreUnmatched;
    }

    public CsvBindingCollection<TValue> Bind<TValue>(ReadOnlyMemory<T> line)
    {
        Options.MakeReadOnly();

        HeaderData headerData = DefaultHeaderBinder<T>.GetHeaderDataFor<TValue>();
        List<CsvBinding<TValue>> foundBindings = new();
        SpanPredicate<T>? ignorePredicate = headerData.Ignore;

        ArrayPool<T> arrayPool = Options.ArrayPool.AllocatingIfNull();
        T[]? buffer = null;

        try
        {
            CsvEnumerationStateRef<T> state = new(Options, line);

            while (state.TryGetField(out ReadOnlyMemory<T> fieldMemory))
            {
                ReadOnlySpan<T> field = fieldMemory.Span;
                int index = foundBindings.Count;

                if (ignorePredicate is not null && ignorePredicate(field))
                {
                    foundBindings.Add(CsvBinding.Ignore<TValue>(index));
                    continue;
                }

                CsvBinding<TValue>? binding = null;

                foreach (ref readonly var candidate in headerData.Candidates)
                {
                    if (Options.SequenceEqual(candidate.Value, field))
                    {
                        binding = CsvBinding.FromHeaderBinding<TValue>(candidate.Target, index);
                        break;

                    }
                }

                if (binding is null && !IgnoreUnmatched)
                {
                    throw new CsvBindingException(
                        $"Column {foundBindings.Count} could not be bound to a member of {typeof(TValue)}: Column " +
                        field.AsPrintableString(Options.AllowContentInExceptions, state.Dialect) +
                        ", Line: " + line.Span.AsPrintableString(Options.AllowContentInExceptions, state.Dialect));
                }

                foundBindings.Add(binding ?? CsvBinding.Ignore<TValue>(index: foundBindings.Count));
            }
        }
        finally
        {
            arrayPool.EnsureReturned(ref buffer);
        }

        return new CsvBindingCollection<TValue>(foundBindings);
    }

    /// <summary>
    /// Returns members of <typeparamref name="TValue"/> that can be used for binding.
    /// </summary>
    /// <seealso cref="CsvHeaderAttribute"/>
    /// <seealso cref="CsvHeaderExcludeAttribute"/>
    private static HeaderData GetHeaderDataFor<TValue>()
    {
        if (!_candidateCache.TryGetValue(typeof(TValue), out var headerData))
        {
            var typeInfo = CsvTypeInfo<TValue>.Instance;

            List<HeaderBindingCandidate> candidates = new();

            foreach (var member in typeInfo.Members)
            {
                if (member.IsReadOnly)
                    continue;

                bool isExcluded = false;
                CsvHeaderAttribute? attr = null;

                foreach (var attribute in member.Attributes)
                {
                    if (attribute is CsvHeaderExcludeAttribute)
                    {
                        isExcluded = true;
                        break;
                    }

                    if (attribute is CsvHeaderAttribute match)
                    {
                        attr = match;
                        break;
                    }
                }

                if (isExcluded)
                {
                    continue;
                }

                if (attr is not null)
                {
                    candidates.EnsureCapacity(candidates.Count + attr.Values.Length);
                    foreach (var value in attr.Values)
                    {
                        candidates.Add(new HeaderBindingCandidate(value, member.Value, attr.Order));
                    }
                }
                else
                {
                    candidates.Add(new HeaderBindingCandidate(member.Value.Name, member.Value, default));
                }
            }

            CsvHeaderIgnoreAttribute? ignoreAttribute = null;

            foreach (var attribute in typeInfo.Attributes)
            {
                if (attribute is CsvHeaderTargetAttribute { } attr)
                {
                    var member = typeInfo.GetPropertyOrField(attr.MemberName);
                    candidates.EnsureCapacity(candidates.Count + attr.Values.Length);

                    foreach (var value in attr.Values)
                    {
                        candidates.Add(new HeaderBindingCandidate(value, member.Value, attr.Order));
                    }
                }
                else
                {
                    ignoreAttribute ??= attribute as CsvHeaderIgnoreAttribute;
                }
            }

            SpanPredicate<T>? predicate = ignoreAttribute is not null
                ? HeaderMatcherDefaults.CheckIgnore<T>(ignoreAttribute.Values!, ignoreAttribute.Comparison)
                : null;

            foreach (var parameter in typeInfo.ConstructorParameters)
            {
                CsvHeaderAttribute? attr = null;

                foreach (var attribute in parameter.Attributes)
                {
                    if (attribute is CsvHeaderAttribute match)
                    {
                        attr = match;
                        break;
                    }
                }

                if (attr is not null)
                {
                    candidates.EnsureCapacity(candidates.Count + attr.Values.Length);
                    foreach (var value in attr.Values)
                    {
                        candidates.Add(new HeaderBindingCandidate(value, parameter.Value, attr.Order));
                    }
                }
                else
                {
                    candidates.Add(new HeaderBindingCandidate(parameter.Value.Name!, parameter.Value, default));
                }
            }

            candidates.AsSpan().Sort(); // sorted by Order

            _candidateCache.AddOrUpdate(typeof(TValue), headerData = new HeaderData(predicate, candidates));
        }

        return headerData;
    }
}
