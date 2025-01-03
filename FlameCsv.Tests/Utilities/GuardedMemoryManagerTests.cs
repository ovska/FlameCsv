using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;
using Xunit.Abstractions;

namespace FlameCsv.Tests.Utilities;

[SupportedOSPlatform("windows")]
public class GuardedMemoryManagerTests
{
    [Theory, MemberData(nameof(AllocationData))]
    public void Should_Allocate_Guarded_Memory(bool fromEnd, int size)
    {
        // $env:COMPlus_legacyCorruptedStateExceptionsPolicy=1
        // dotnet test --filter Allocate

        using var memoryManager = new GuardedMemoryManager(size, fromEnd);

        var span = memoryManager.GetSpan();

        try
        {
            Assert.Equal(0, span[0]);
        }
        catch (AccessViolationException ave)
        {
            Assert.Fail($"{memoryManager}\nFirst: 0 / {span.Length} " + ave.Message);
        }

        try
        {
            Assert.Equal(0, span[^1]);
        }
        catch (AccessViolationException ave)
        {
            Assert.Fail($"{memoryManager}\nLast {span.Length - 1} / {span.Length}: " + ave.Message);
        }

        // ref byte x = ref span.DangerousGetReferenceAt(size + 1);
        // Assert.Equal(0, x);
    }

    public static TheoryData<bool, int> AllocationData
    {
        get
        {
            var data = new TheoryData<bool, int>();

            var items =
                from fromEnd in (bool[])[true, false]
                from size in (int[])[32, 64, 1024, 4096, 4096 * 2]
                select (fromEnd, size);

            foreach (var (fromEnd, size) in items)
            {
                data.Add(fromEnd, size);
            }

            return data;
        }
    }
}
