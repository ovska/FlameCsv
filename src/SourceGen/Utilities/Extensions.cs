using System.Collections;
using System.Runtime.CompilerServices;
using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen.Utilities;

internal static class UtilityExtensions
{
    public static ulong AsUInt64Bits(in this BigInteger bigInteger)
    {
        if (bigInteger.IsZero)
            return 0;

        if (bigInteger >= 0)
            return (ulong)bigInteger;

        long value = (long)bigInteger;
        return Unsafe.As<long, ulong>(ref value);
    }

    public static void ReportDiagnostics(in this SourceProductionContext context, Diagnostic[] diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }
    }

    public static EquatableArray<IMemberModel>.WhereEnumerable Writable(in this EquatableArray<IMemberModel> members)
    {
        return members.Where(static m => m.IsFormattable);
    }

    public static EquatableArray<IMemberModel>.WhereEnumerable Readable(in this EquatableArray<IMemberModel> members)
    {
        return members.Where(static m => m.IsParsable);
    }

    public static bool TryGetNamedArgument(this AttributeData attribute, string name, out TypedConstant value)
    {
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == name)
            {
                value = argument.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    public static IEnumerable<T> DistinctBy<T, TValue>(this IEnumerable<T> values, Func<T, TValue> selector)
        where TValue : IEquatable<TValue>
    {
        HashSet<TValue> set = PooledSet<TValue>.Acquire();

        try
        {
            foreach (var value in values)
            {
                if (set.Add(selector(value)))
                {
                    yield return value;
                }
            }
        }
        finally
        {
            PooledSet<TValue>.Release(set);
        }
    }

    public static IGrouping<TKey, TElement> AsGrouping<TKey, TElement>(this IEnumerable<TElement> source, TKey key)
    {
        return new Grouping<TKey, TElement>(key, source);
    }

    private sealed class Grouping<TKey, TElement>(TKey key, IEnumerable<TElement> elements) : IGrouping<TKey, TElement>
    {
        public TKey Key => key;

        public IEnumerator<TElement> GetEnumerator() => elements.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
