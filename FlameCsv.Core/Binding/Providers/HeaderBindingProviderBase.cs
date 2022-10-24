using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using FlameCsv.Binding.Attributes;
using FlameCsv.Extensions;
using FlameCsv.Readers;
using FlameCsv.Readers.Internal;

namespace FlameCsv.Binding.Providers;

public abstract class HeaderBindingProviderBase<T, TResult> : ICsvHeaderBindingProvider<T>
    where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Columns that could not be matched are ignored.
    /// </summary>
    public bool IgnoreNonMatched { get; }

    private readonly CsvHeaderMatcher<T> _matcher;
    private List<CsvBinding>? _bindings;

    public bool ReadHeader => true;

    protected bool _headerRead;

    protected HeaderBindingProviderBase(
        CsvHeaderMatcher<T> matcher,
        bool ignoreNonMatched)
    {
        ArgumentNullException.ThrowIfNull(matcher);

        _matcher = matcher;
        IgnoreNonMatched = ignoreNonMatched;
    }

    public virtual bool TryProcessHeader(ReadOnlySpan<T> line, CsvConfiguration<T> configuration)
    {
        var candidates = GetBindingCandidates().ToArray().AsSpan();
        candidates.Sort(static (a, b) => b.Order.CompareTo(a.Order));

        List<CsvBinding> foundBindings = new();
        int index = 0;

        using var buffer = new BufferOwner<T>(configuration.Security);
        var enumerator = new CsvColumnEnumerator<T>(
            line,
            in configuration._options,
            columnCount: null,
            quoteCount: line.Count(configuration._options.StringDelimiter),
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

            if (!found && IgnoreNonMatched)
            {
                foundBindings.Add(CsvBinding.Ignore(index));
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
        var members = typeof(TResult).GetMembers(CsvBindingConstants.MemberLookupFlags)
            .Where(m => m is PropertyInfo or FieldInfo && !m.HasAttribute<HeaderBindingIgnoreAttribute>())
            .SelectMany(
                static m => m.GetCustomAttribute<HeaderBindingAttribute>() is { } attr
                    ? attr.Values.Select(v => (v, m, attr.Order))
                    : Enumerable.Repeat((m.Name, m, 0), 1));

        var targeted = typeof(TResult).GetCustomAttributes<HeaderBindingTargetAttribute>()
            .SelectMany(attr => attr.GetMembers(typeof(TResult)));

        return members.Concat(targeted);
    }
}
