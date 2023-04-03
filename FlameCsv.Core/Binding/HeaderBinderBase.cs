using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

public abstract class HeaderBinderBase<T> : IHeaderBinder<T>
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

    private readonly ConditionalWeakTable<Type, HeaderData> _candidateCache = new();

    /// <summary>
    /// Columns that could not be matched are ignored.
    /// </summary>
    public bool IgnoreUnmatched { get; }

    private readonly ICsvHeaderMatcher<T> _matcher;

    internal protected HeaderBinderBase(
        ICsvHeaderMatcher<T> matcher,
        bool ignoreUnmatched)
    {
        ArgumentNullException.ThrowIfNull(matcher);

        _matcher = matcher;
        IgnoreUnmatched = ignoreUnmatched;
    }

    public CsvBindingCollection<TValue> Bind<TValue>(ReadOnlySpan<T> line, CsvReaderOptions<T> options)
    {
        HeaderData headerData = GetBindingCandidates<TValue>();

        List<CsvBinding<TValue>> foundBindings = new();

        using var bufferOwner = new BufferOwner<T>(options);

        CsvDialect<T> dialect = new(options);

        CsvColumnEnumerator<T> enumerator = new(
            line,
            in dialect,
            columnCount: null,
            quoteCount: line.Count(options.Quote),
            new ValueBufferOwner<T>(ref bufferOwner._array, options.ArrayPool ?? AllocatingArrayPool<T>.Instance));

        SpanPredicate<T>? ignorePredicate = headerData.Ignore;

        foreach (var column in enumerator)
        {
            int index = foundBindings.Count;

            if (ignorePredicate is not null && ignorePredicate(column))
            {
                foundBindings.Add(CsvBinding.Ignore<TValue>(index));
                continue;
            }

            CsvBinding<TValue>? binding = null;

            foreach (ref readonly var candidate in headerData.Candidates)
            {
                HeaderBindingArgs args = new(index, candidate.Value, candidate.Target, candidate.Order);

                binding = _matcher.TryMatch<TValue>(column, in args);

                if (binding is not null)
                    break;
            }

            if (binding is null && !IgnoreUnmatched)
            {
                throw new CsvBindingException(
                    $"Column {index} could not be bound to a member of {typeof(TValue)}: Column " +
                    column.AsPrintableString(options.AllowContentInExceptions, in dialect) +
                    ", Line: " + line.AsPrintableString(options.AllowContentInExceptions, in dialect));
            }

            foundBindings.Add(binding ?? CsvBinding.Ignore<TValue>(index));
        }

        return new CsvBindingCollection<TValue>(foundBindings);
    }

    /// <summary>
    /// Returns members of <typeparamref name="TValue"/> that can be used for binding.
    /// </summary>
    /// <seealso cref="CsvHeaderAttribute"/>
    /// <seealso cref="CsvHeaderExcludeAttribute"/>
    private HeaderData GetBindingCandidates<TValue>()
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
