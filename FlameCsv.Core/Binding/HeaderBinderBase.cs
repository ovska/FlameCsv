using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Readers;
using FlameCsv.Readers.Internal;

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

    private readonly CsvHeaderMatcher<T> _matcher;

    protected HeaderBinderBase(
        CsvHeaderMatcher<T> matcher,
        bool ignoreUnmatched)
    {
        ArgumentNullException.ThrowIfNull(matcher);

        _matcher = matcher;
        IgnoreUnmatched = ignoreUnmatched;
    }

    public CsvBindingCollection<TValue> Bind<TValue>(ReadOnlySpan<T> line, CsvReaderOptions<T> options)
    {
        HeaderData headerData = GetBindingCandidates<TValue>();

        List<CsvBinding> foundBindings = new();
        int index = 0;

        using var bufferOwner = new BufferOwner<T>(options);

        CsvColumnEnumerator<T> enumerator = new(
            line,
            in options.tokens,
            columnCount: null,
            quoteCount: line.Count(options.tokens.StringDelimiter),
            new ValueBufferOwner<T>(ref bufferOwner._array, options.ArrayPool));

        ReadOnlySpan<HeaderBindingCandidate> candidates = headerData.Candidates.AsSpan();
        SpanPredicate<T>? ignorePredicate = headerData.Ignore;

        // TODO: clean up this loop
        while (enumerator.MoveNext())
        {
            if (ignorePredicate is not null && ignorePredicate(enumerator.Current))
            {
                foundBindings.Add(CsvBinding.Ignore(index));
                index++;
                continue;
            }

            bool found = false;

            foreach (ref readonly var candidate in candidates)
            {
                HeaderBindingArgs args = new()
                {
                    Member = candidate.Member,
                    Order = candidate.Order,
                    Value = candidate.Value,
                    TargetType = typeof(TValue),
                    Index = index,
                };

                if (_matcher(enumerator.Current, in args) is CsvBinding binding)
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
                    throw new CsvBindingException($"Column {index} could not be bound to a member of {typeof(TValue)}");
                }

                foundBindings.Add(CsvBinding.Ignore(index));
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
        if (!_candidateCache.TryGetValue(typeof(TValue), out var candidates))
        {
            var members = typeof(TValue).GetCachedPropertiesAndFields()
                .Where(m => !m.HasAttribute<CsvHeaderExcludeAttribute>())
                .SelectMany(
                    static m => m.HasAttribute<CsvHeaderAttribute>(out var attr)
                        ? attr.Values.Select(v => new HeaderBindingCandidate(v, m, attr.Order))
                        : Enumerable.Repeat(new HeaderBindingCandidate(m.Name, m, 0), 1));

            var targeted = typeof(TValue).GetCachedCustomAttributes()
                .OfType<CsvHeaderTargetAttribute>()
                .SelectMany(static attr => attr.GetMembers(typeof(TValue)));

            var candidatesList = members.Concat(targeted).ToList();
            candidatesList.AsSpan().Sort(static (a, b) => b.Order.CompareTo(a.Order));

            var ignored = typeof(TValue).GetCachedCustomAttributes()
                .OfType<CsvHeaderIgnoreAttribute>()
                .FirstOrDefault();

            SpanPredicate<T>? predicate = ignored is not null
                ? HeaderMatcherDefaults.CheckIgnore<T>(ignored.Values!, ignored.Comparison)
                : null;

            _candidateCache.AddOrUpdate(typeof(TValue), candidates = new HeaderData(predicate, candidatesList));
        }

        return candidates;
    }
}
