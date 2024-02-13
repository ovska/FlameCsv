using System.Diagnostics.CodeAnalysis;
using FlameCsv.Binding.Attributes;
using FlameCsv.Extensions;

namespace FlameCsv.Reflection;

internal abstract class ReflectionData
{
    public abstract ReadOnlySpan<object> Attributes { get; }

    public bool IsExcluded(bool write)
    {
        foreach (var attr in Attributes)
        {
            if (attr is CsvHeaderExcludeAttribute exclude &&
                exclude.Scope.IsValidFor(write))
            {
                return true;
            }
        }

        return false;
    }
}
