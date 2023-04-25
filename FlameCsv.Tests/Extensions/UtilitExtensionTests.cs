using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using FlameCsv.Extensions;

namespace FlameCsv.Tests.Extensions;

public static class UtilitExtensionTests
{
    [Fact]
    public static void Should_Make_Copy_Of_Data()
    {
        var first = new int[] { 0, 1, 2 };

        var mem = new ReadOnlyMemory<int>(first);
        var copy = mem.SafeCopy();

        first[0] = 2;

        Assert.NotEqual(first, copy.ToArray());
    }

    [Fact]
    public static void Should_Reuse_String_Memory_Instances()
    {
        var first = "Test".AsMemory();
        var second = first.SafeCopy();

        Assert.True(MemoryMarshal.TryGetString(first, out string? s1, out _, out _));
        Assert.True(MemoryMarshal.TryGetString(second, out string? s2, out _, out _));
        Assert.Same(s1, s2);
    }
}
