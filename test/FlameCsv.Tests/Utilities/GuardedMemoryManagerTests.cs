using System.Runtime.Versioning;

// ReSharper disable UnusedMember.Global

namespace FlameCsv.Tests.Utilities;

public class GuardedMemoryManagerCharTests : GuardedMemoryManagerTestsBase<char>
{
    public static bool IsNotWindows => !OperatingSystem.IsWindows();
}

public class GuardedMemoryManagerByteTests : GuardedMemoryManagerTestsBase<byte>
{
    public static bool IsNotWindows => !OperatingSystem.IsWindows();
}

[SupportedOSPlatform("windows")]
public abstract class GuardedMemoryManagerTestsBase<T> where T : unmanaged
{
    [Theory(Skip = "GMM is windows only", SkipWhen = "IsNotWindows"), MemberData(nameof(AllocationData))]
    public void Should_Allocate_Guarded_Memory(bool fromEnd, int size)
    {
        Assert.Equal(4096, Environment.SystemPageSize);

        // $env:COMPlus_legacyCorruptedStateExceptionsPolicy=1
        // dotnet test --filter Allocate -- xUnit.StopOnFail=true

        using var memoryManager = new GuardedMemoryManager<T>(size, fromEnd);

        var span = memoryManager.GetSpan();

        try
        {
            Assert.Equal(default, span[0]);
        }
        catch (AccessViolationException ave)
        {
            Assert.Fail($"{memoryManager}\nFirst: 0 / {span.Length} " + ave.Message);
        }

        try
        {
            Assert.Equal(default, span[^1]);
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

            foreach (var fromEnd in GlobalData.Booleans)
            foreach (var size in (int[]) [32, 64, 1024, 4096, 4096 * 2])
            {
                data.Add(fromEnd, size);
            }

            return data;
        }
    }
}
