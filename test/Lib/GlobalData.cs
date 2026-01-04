using System.Diagnostics;

namespace FlameCsv.Tests;

public static class GlobalData
{
    /// <summary>
    /// True and false.
    /// </summary>
    public static bool[] Booleans { get; } = [true, false];

    /// <summary>
    /// Guarded memory allocation data.
    /// <see langword="null"/> means non-guarded date,
    /// <c>true</c> means data is guarded right after the memory region,
    /// <c>false</c> means the guarded data is right before.
    /// </summary>
    public static PoisonPagePlacement[] PoisonPlacement { get; } = InitializePlacement();

    public static T[] Enum<T>()
        where T : struct, Enum => System.Enum.GetValues<T>();

    private static PoisonPagePlacement[] InitializePlacement()
    {
        PoisonPagePlacement[] arr = [PoisonPagePlacement.None];
        Smoke(ref arr);
        return arr;

        [Conditional("FUZZ"), Conditional("FULL_TEST_SUITE")]
        static void Smoke(ref PoisonPagePlacement[] arr)
        {
            arr = [PoisonPagePlacement.None, PoisonPagePlacement.After, PoisonPagePlacement.Before];
        }
    }
}
