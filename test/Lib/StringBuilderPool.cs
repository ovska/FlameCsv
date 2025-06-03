using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace FlameCsv.Tests;

public static class StringBuilderPool
{
    public static readonly ObjectPool<StringBuilder> Value = ObjectPool.Create(
        new StringBuilderPooledObjectPolicy
        {
            InitialCapacity = short.MaxValue * 4,
            MaximumRetainedCapacity = short.MaxValue * 4,
        }
    );
}
