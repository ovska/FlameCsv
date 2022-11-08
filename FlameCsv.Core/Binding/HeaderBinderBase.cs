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
    private readonly ConditionalWeakTable<Type, List<HeaderBindingCandidate>> _candidateCache = new();

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
        var candidates = GetBindingCandidates<TValue>().AsSpan();

        List<CsvBinding> foundBindings = new();
        int index = 0;

        using var buffer = new BufferOwner<T>(options.Security);

        var enumerator = new CsvColumnEnumerator<T>(
            line,
            in options.tokens,
            columnCount: null,
            quoteCount: line.Count(options.tokens.StringDelimiter),
            ref buffer._array);

        foreach (var value in enumerator)
        {
            bool found = false;

            foreach (ref var candidate in candidates)
            {
                CsvBinding? binding = _matcher(
                    new HeaderBindingArgs
                    {
                        Member = candidate.Member,
                        Order = candidate.Order,
                        Value = candidate.Value,
                        TargetType = typeof(TValue),
                        Index = index,
                    },
                    value);

                if (binding.HasValue)
                {
                    foundBindings.Add(binding.Value);
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
    /// <seealso cref="CsvHeaderIgnoreAttribute"/>
    internal List<HeaderBindingCandidate> GetBindingCandidates<TValue>()
    {
        if (!_candidateCache.TryGetValue(typeof(TValue), out var candidates))
        {
            var members = typeof(TValue).GetCachedPropertiesAndFields()
                .Where(m => !m.HasAttribute<CsvHeaderIgnoreAttribute>())
                .SelectMany(
                    static m => m.HasAttribute<CsvHeaderAttribute>(out var attr)
                        ? attr.Values.Select(v => new HeaderBindingCandidate(v, m, attr.Order))
                        : Enumerable.Repeat(new HeaderBindingCandidate(m.Name, m, 0), 1));

            var targeted = typeof(TValue).GetCachedCustomAttributes()
                .OfType<CsvHeaderTargetAttribute>()
                .SelectMany(attr => attr.GetMembers(typeof(TValue)));

            candidates = members.Concat(targeted).ToList();
            candidates.AsSpan().Sort(static (a, b) => b.Order.CompareTo(a.Order));
            _candidateCache.AddOrUpdate(typeof(TValue), candidates);
        }

        return candidates;
    }
}
