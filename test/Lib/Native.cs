using System.Runtime.InteropServices;

namespace FlameCsv.Tests;

public static partial class Native
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint VirtualAlloc(nint lpAddress, nuint dwSize, uint flAllocationType, uint flProtect);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VirtualFree(nint lpAddress, nuint dwSize, uint dwFreeType);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VirtualProtect(nint lpAddress, nuint dwSize, uint flNewProtect, out uint lpflOldProtect);
}
