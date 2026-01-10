using System.Runtime.InteropServices;
using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal static class IndexBindingModel
{
    public static EquatableArray<IMemberModel?> Resolve(
        bool write,
        in FlameSymbols symbols,
        EquatableArray<IMemberModel> members,
        ref AnalysisCollector collector
    )
    {
        // we don't bother checking these if there are errors already
        // this also avoids returning index binding problems twice
        if (collector.Diagnostics.Count != 0)
        {
            return [];
        }

        SortedDictionary<int, IMemberModel?>? dict = null;

        try
        {
            foreach (var ignored in collector.IgnoredIndexes)
            {
                dict ??= MemberDictPool.Acquire();
                dict[ignored] = null;
            }

            foreach (var member in members)
            {
                if (member.Index is not { } index)
                    continue;
                if (write ? !member.IsFormattable : !member.IsParsable)
                    continue;

                dict ??= MemberDictPool.Acquire();

                // first on this index
                if (!dict.TryGetValue(index, out IMemberModel? existing))
                {
                    dict[index] = member.IsIgnored ? null : member;
                    continue;
                }

                if (member.IsIgnored)
                {
                    // current is ignored, don't touch existing
                    continue;
                }

                // check if existing is ignored, overwrite with current
                if (existing is null)
                {
                    dict[index] = member;
                    continue;
                }

                // conflicting bindings
                collector.AddDiagnostic(
                    Diagnostics.ConflictingIndex(symbols.TargetType, $"{member.Identifier} ({member.Kind})")
                );
                return [];
            }

            // check indexes for gaps
            if (dict is not null)
            {
                int expected = 0;

                foreach (var kvp in dict)
                {
                    if (kvp.Key != expected)
                    {
                        // gap in the indexes
                        collector.AddDiagnostic(Diagnostics.GapInIndex(symbols.TargetType, expected));
                        return [];
                    }

                    expected++;
                }

                // valid
                IMemberModel?[] result = new IMemberModel?[dict.Count];

                foreach (var kvp in dict)
                {
                    result[kvp.Key] = kvp.Value;
                }

                return ImmutableCollectionsMarshal.AsImmutableArray(result);
            }

            return [];
        }
        finally
        {
            MemberDictPool.Release(dict);
        }
    }
}
