using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Readers;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Binding.Providers;

public enum UnmatchedHeaderBindingBehavior
{
    /// <summary>Requires all columns to match for the provider to report a successful binding.</summary>
    RequireAll = 0,

    /// <summary>Returns an ignored <see cref="CsvBinding"/> for columns that could not be matched.</summary>
    Ignore = 1,

    /// <summary>Throws an exception if a column cannot be bound.</summary>
    Throw = 2,
}

public abstract class HeaderBindingProviderBase<T, TResult> : ICsvHeaderBindingProvider<T>
    where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Columns that could not be matched are ignored.
    /// </summary>
    public UnmatchedHeaderBindingBehavior UnmatchedBehavior { get; }

    private readonly CsvHeaderMatcher<T> _matcher;
    private List<CsvBinding>? _bindings;

    public bool ReadHeader => true;

    protected bool _headerRead;

    protected HeaderBindingProviderBase(
        CsvHeaderMatcher<T> matcher,
        UnmatchedHeaderBindingBehavior unmatchedBehavior)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        GuardEx.IsDefined(unmatchedBehavior);

        _matcher = matcher;
        UnmatchedBehavior = unmatchedBehavior;
    }

    public virtual bool TryProcessHeader(ReadOnlySpan<T> line, CsvReaderOptions<T> readerOptions)
    {
        var candidates = GetBindingCandidates().ToArray().AsSpan();
        candidates.Sort(static (a, b) => b.Order.CompareTo(a.Order));

        List<CsvBinding> foundBindings = new();
        int index = 0;

        using var buffer = new BufferOwner<T>(readerOptions.Security);

        var enumerator = new CsvColumnEnumerator<T>(
            line,
            in readerOptions.tokens,
            columnCount: null,
            quoteCount: line.Count(readerOptions.tokens.StringDelimiter),
            buffer);

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
                        TargetType = typeof(TResult),
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
                switch (UnmatchedBehavior)
                {
                    case UnmatchedHeaderBindingBehavior.Ignore:
                        foundBindings.Add(CsvBinding.Ignore(index));
                        break;
                    case UnmatchedHeaderBindingBehavior.Throw:
                        throw new CsvBindingException(
                            $"Column {index} could not be bound to a member of {typeof(TResult)}");
                    case UnmatchedHeaderBindingBehavior.RequireAll:
                    default:
                        return false;
                }
            }

            index++;
        }

        if (foundBindings.Count == 0)
            return false;

        _bindings = foundBindings;
        return true;
    }

    public virtual bool TryGetBindings<TValue>(
        [NotNullWhen(true)] out CsvBindingCollection<TValue>? bindings)
    {
        if (_bindings is null)
            ThrowHelper.ThrowInvalidOperationException("Header not read");

        bindings = new(_bindings);
        return true;
    }

    /// <summary>
    /// Returns members of <typeparamref name="TResult"/> that can be used for binding.
    /// Default implementation takes into account <see cref="HeaderBindingAttribute"/> and
    /// <see cref="HeaderBindingIgnoreAttribute"/>.
    /// </summary>
    protected virtual IEnumerable<(string Value, MemberInfo Member, int Order)> GetBindingCandidates()
    {
        var members = typeof(TResult).GetCachedPropertiesAndFields()
            .Where(m => !m.HasAttribute<HeaderBindingIgnoreAttribute>())
            .SelectMany(
                static m => m.HasAttribute<HeaderBindingAttribute>(out var attr)
                    ? attr.Values.Select(v => (v, m, attr.Order))
                    : Enumerable.Repeat((m.Name, m, 0), 1));

        var targeted = typeof(TResult).GetCachedCustomAttributes()
            .OfType<HeaderBindingTargetAttribute>()
            .SelectMany(attr => attr.GetMembers(typeof(TResult)));

        return members.Concat(targeted);
    }
}
