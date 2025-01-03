using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FlameCsv.Tests;

[SupportedOSPlatform("windows")]
public sealed partial class GuardedMemoryManager : MemoryManager<byte>
{
    private readonly nint _baseAddress;
    private readonly nint _memoryPointer;
    private readonly int _totalSize;
    private readonly int _size;
    private readonly int _pageSize;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint VirtualAlloc(nint lpAddress, nuint dwSize, uint flAllocationType, uint flProtect);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualFree(nint lpAddress, nuint dwSize, uint dwFreeType);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool VirtualProtect(nint lpAddress, nuint dwSize, uint flNewProtect, out uint lpflOldProtect);

    private const uint MEM_COMMIT = 0x00001000;
    private const uint MEM_RESERVE = 0x00002000;
    private const uint MEM_RELEASE = 0x00008000;
    private const uint PAGE_READWRITE = 0x04;
    private const uint PAGE_NOACCESS = 0x01;

    public GuardedMemoryManager(int size, bool fromEnd = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);

        _pageSize = Environment.SystemPageSize;
        _size = size;

        int pagesInBlock = Math.Max(1, size/ _pageSize);
        int totalPages = pagesInBlock + 2;
        _totalSize = _pageSize * totalPages; // Guard regions + block size

        // Allocate memory
        _baseAddress = VirtualAlloc(nint.Zero, (nuint)_totalSize, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

        if (_baseAddress == nint.Zero)
        {
            ThrowException("Failed to allocate memory.");
            return;
        }

        _memoryPointer = !fromEnd
            ? _baseAddress + _pageSize
            : (_baseAddress + _totalSize - _pageSize - size);

        if (!VirtualProtect(_baseAddress, (nuint)_pageSize, PAGE_NOACCESS, out _))
        {
            ThrowException("Failed to protect the lower guard region.");
            return;
        }

        nint upperGuard = _baseAddress + _pageSize + _pageSize * pagesInBlock;
        if (!VirtualProtect(upperGuard, (nuint)_pageSize, PAGE_NOACCESS, out _))
        {
            ThrowException("Failed to protect the upper guard region.");
            return;
        }

        void ThrowException(string message)
        {
            throw new InvalidOperationException(
                $"{message}\n" +
                $"Sizes of page: {_pageSize}, block: {_size}, pages in block: {pagesInBlock}, total: {_totalSize} in {totalPages} pages\n" +
                $"Address base: {(ulong)_baseAddress}, memory: {(ulong)_memoryPointer} (diff: {(ulong)(_memoryPointer - _baseAddress)})\n" +
                $"PInvoke error: {Marshal.GetLastPInvokeError()} {Marshal.GetLastPInvokeErrorMessage()}");
        }
    }

    public unsafe override Span<byte> GetSpan()
    {
        return new Span<byte>((void*)_memoryPointer, _size);
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (elementIndex < 0 || elementIndex >= _size)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        }

        unsafe
        {
            byte* ptr = (byte*)_memoryPointer + elementIndex;
            return new MemoryHandle(ptr);
        }
    }

    public override void Unpin() { }

    protected override void Dispose(bool disposing)
    {
        if (_baseAddress != nint.Zero)
        {
            VirtualFree(_baseAddress, nuint.Zero, MEM_RELEASE);
        }
    }

    public override string ToString()
    {
        nint start = _memoryPointer - _baseAddress;
        nint end = start + _size;
        return $"GuardedMemoryManager {{ Page: {_pageSize}, Memory range: {start}..{end}, Total range: 0..{_totalSize} }}";
    }
}

