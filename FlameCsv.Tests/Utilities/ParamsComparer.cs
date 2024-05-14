using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlameCsv.Tests.Utilities;

internal sealed class ParamsComparer : IEqualityComparer<object[]>
{
    public bool Equals(object[]? x, object[]? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        
        if (x is null || y is null || x.Length != y.Length)
            return false;

        return x.Zip(y).All((t) => t.First.Equals(t.Second));
    }

    public int GetHashCode([DisallowNull] object[] obj)
    {
        HashCode hc = new();

        foreach (var value in obj)
        {
            hc.Add(value?.GetHashCode() ?? 0);
        }

        return hc.ToHashCode();
    }
}
