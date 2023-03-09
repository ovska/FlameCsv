using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Readers;
using FlameCsv.Readers.Internal;
using FlameCsv.Reflection;

namespace FlameCsv.Binding;

public abstract class HeaderBinderBase<T> : IHeaderBinder<T>
    where T : unmanaged, IEquatable<T>
{
    private sealed class HeaderData
    {
        public SpanPredicate<T>? Ignore { get; }
        public List<HeaderBindingCandidate> Candidates { get; }

        public HeaderData(SpanPredicate<T>? ignore, List<HeaderBindingCandidate> candidates)
        {
            Ignore = ignore;
            Candidates = candidates;
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
        int index = 0;

        using var bufferOwner = new BufferOwner<T>(options);

        CsvColumnEnumerator<T> enumerator = new(
            line,
            in options.tokens,
            columnCount: null,
            quoteCount: line.Count(options.tokens.StringDelimiter),
            new ValueBufferOwner<T>(ref bufferOwner._array, options.ArrayPool ?? AllocatingArrayPool<T>.Instance));

        ReadOnlySpan<HeaderBindingCandidate> candidates = headerData.Candidates.AsSpan();
        SpanPredicate<T>? ignorePredicate = headerData.Ignore;

        while (enumerator.MoveNext())
        {
            if (ignorePredicate is not null && ignorePredicate(enumerator.Current))
            {
                foundBindings.Add(CsvBinding.Ignore<TValue>(index));
                index++;
                continue;
            }

            bool found = false;

            foreach (ref readonly var candidate in candidates)
            {
                HeaderBindingArgs args = new(index, candidate.Value, candidate.Target, candidate.Order);

                CsvBinding<TValue>? binding = _matcher.TryMatch<TValue>(enumerator.Current, in args);

                if (binding is not null)
                {
                    foundBindings.Add(binding);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                if (!IgnoreUnmatched)
                {
                    throw new CsvBindingException(
                        $"Column {index} could not be bound to a member of {typeof(TValue)}: Column " +
                        enumerator.Current.AsPrintableString(options.AllowContentInExceptions, in options.tokens) +
                        ", Line: " + line.AsPrintableString(options.AllowContentInExceptions, in options.tokens));
                }

                foundBindings.Add(CsvBinding.Ignore<TValue>(index));
            }

            index++;
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
