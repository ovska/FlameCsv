using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// ReSharper disable InconsistentNaming

namespace FlameCsv.Tests.Utilities;

[SupportedOSPlatform("windows")]
public sealed class GuardedMemoryManager<T> : MemoryManager<T> where T : unmanaged
{
    public bool IsDisposed { get; private set; }

    private readonly nint _baseAddress;
    private readonly nint _memoryPointer;
    private readonly int _totalSize; // total size in bytes
    private readonly int _length; // length in T
    private readonly int _pageSize; // O/S page size in bytes

    public unsafe int ByteSize => _length * (sizeof(T) / sizeof(byte));

    private const uint MEM_COMMIT = 0x00001000;
    private const uint MEM_RESERVE = 0x00002000;
    private const uint MEM_RELEASE = 0x00008000;
    private const uint PAGE_READWRITE = 0x04;
    private const uint PAGE_NOACCESS = 0x01;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="length">Length of the memory of <typeparamref name="T"/> returned (see <see cref="ByteSize"/>)</param>
    /// <param name="fromEnd">Whether the protected memory region is right after the memory, instead of right before</param>
    /// <exception cref="InvalidOperationException"></exception>
    public GuardedMemoryManager(int length, bool fromEnd = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        _pageSize = Environment.SystemPageSize;
        _length = length;

        int pagesInBlock = Math.Max(1, ByteSize / _pageSize);
        int totalPages = pagesInBlock + 2;
        _totalSize = _pageSize * totalPages; // Guard regions + block size

        // Allocate memory
        _baseAddress = Native.VirtualAlloc(nint.Zero, (nuint)_totalSize, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

        if (_baseAddress == nint.Zero)
        {
            throw Fail("Failed to allocate memory.");
        }

        _memoryPointer = !fromEnd
            ? _baseAddress + _pageSize
            : (_baseAddress + _totalSize - _pageSize - ByteSize);

        if (!Native.VirtualProtect(_baseAddress, (nuint)_pageSize, PAGE_NOACCESS, out _))
        {
            throw Fail("Failed to protect the lower guard region.");
        }

        nint upperGuard = _baseAddress + _pageSize + _pageSize * pagesInBlock;

        if (!Native.VirtualProtect(upperGuard, (nuint)_pageSize, PAGE_NOACCESS, out _))
        {
            throw Fail("Failed to protect the upper guard region.");
        }

        Exception Fail(string message)
        {
            string fullMsg =
                $"{message}\n"
                + $"Sizes of page: {_pageSize}, block: {ByteSize}, pages in block: {pagesInBlock}, total: {_totalSize} in {totalPages} pages\n"
                + $"Address base: {(ulong)_baseAddress}, memory: {(ulong)_memoryPointer} (diff: {(ulong)(_memoryPointer - _baseAddress)})\n"
                + $"PInvoke error: {Marshal.GetLastPInvokeError()} {Marshal.GetLastPInvokeErrorMessage()}";

            Dispose(true);

            return new InvalidOperationException(fullMsg);
        }
    }

    public override unsafe Span<T> GetSpan()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return new Span<T>((void*)_memoryPointer, _length);
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (elementIndex < 0 || elementIndex >= _length)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        }

        unsafe
        {
            byte* ptr = (byte*)_memoryPointer + elementIndex;
            return new MemoryHandle(ptr);
        }
    }

    public override Memory<T> Memory
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            return base.Memory;
        }
    }

    protected override bool TryGetArray(out ArraySegment<T> segment)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        segment = default;
        return false;
    }

    public override void Unpin()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    protected override void Dispose(bool disposing)
    {
        if (IsDisposed)
            return;

        IsDisposed = true;

        if (_baseAddress != nint.Zero)
        {
            Native.VirtualFree(_baseAddress, nuint.Zero, MEM_RELEASE);
        }
    }

    public override string ToString()
    {
        nint start = _memoryPointer - _baseAddress;
        nint end = start + ByteSize;
        return
            $"GuardedMemoryManager {{ Page: {_pageSize}, Memory range: {start}..{end}, Total range: 0..{_totalSize} }}";
    }
}
