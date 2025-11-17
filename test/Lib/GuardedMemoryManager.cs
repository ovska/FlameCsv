using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FlameCsv.Tests;

[SupportedOSPlatform("windows")]
public sealed class GuardedMemoryManager<T> : MemoryManager<T>
    where T : unmanaged
{
    public bool IsDisposed { get; private set; }

    private readonly nint _baseAddress;
    private readonly nint _memoryPointer;
    private readonly int _totalSize; // total size in bytes
    private readonly int _length; // length in T
    private readonly int _pageSize; // O/S page size in bytes

    private const uint MEM_COMMIT = 0x00001000;
    private const uint MEM_RESERVE = 0x00002000;
    private const uint MEM_RELEASE = 0x00008000;
    private const uint PAGE_READWRITE = 0x04;
    private const uint PAGE_NOACCESS = 0x01;

    /// <summary>
    /// Creates a memory manager that allocates memory guarded with <c>PAGE_NOACCESS</c> at the beginning and
    /// end of the returned memory. It is used to validate that data from outside the managed memory and span
    /// types isn't read or written to with unsafe code or manual byte address arithmetic.
    /// </summary>
    /// <param name="length">Length of the memory in <typeparamref name="T"/></param>
    /// <param name="fromEnd">Whether the protected memory region is right after the memory, instead of right before</param>
    /// <exception cref="InvalidOperationException"></exception>
    public unsafe GuardedMemoryManager(int length, bool fromEnd = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        _pageSize = Environment.SystemPageSize;
        _length = length;

        int lengthInBytes = length * sizeof(T);
        int totalPages = 2 + (lengthInBytes + _pageSize - 1) / _pageSize;
        _totalSize = _pageSize * totalPages; // Guard regions + block size

        // Allocate memory
        _baseAddress = Native.VirtualAlloc(nint.Zero, (nuint)_totalSize, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);

        if (_baseAddress == nint.Zero)
        {
            throw Fail("Failed to allocate memory.");
        }

        // data either starts one page from start, or ends one page from end
        _memoryPointer = !fromEnd ? _baseAddress + _pageSize : (_baseAddress + _totalSize - _pageSize - lengthInBytes);

        if (!Native.VirtualProtect(_baseAddress, (nuint)_pageSize, PAGE_NOACCESS, out _))
        {
            throw Fail("Failed to protect the lower guard region.");
        }

        // one page from end
        nint upperGuard = _baseAddress + _totalSize - _pageSize;

        if (!Native.VirtualProtect(upperGuard, (nuint)_pageSize, PAGE_NOACCESS, out _))
        {
            throw Fail("Failed to protect the upper guard region.");
        }

        Exception Fail(string message)
        {
            string fullMsg =
                $"{message}\n"
                + $"Sizes of page: {_pageSize}, block: {lengthInBytes}, reserved {_totalSize} bytes in {totalPages} pages\n"
                + $"Address base: {(ulong)_baseAddress}, memory: {(ulong)_memoryPointer} (diff: {(ulong)(_memoryPointer - _baseAddress)})\n"
                + $"PInvoke error {Marshal.GetLastPInvokeError()}: {Marshal.GetLastPInvokeErrorMessage()}";

            Dispose(true);

            return new InvalidOperationException(fullMsg);
        }
    }

    public override Span<T> GetSpan()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        unsafe
        {
            return new Span<T>((void*)_memoryPointer, _length);
        }
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
            byte* ptr = (byte*)_memoryPointer + elementIndex * sizeof(T);
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

        try
        {
            if (_baseAddress != nint.Zero)
            {
                Native.VirtualFree(_baseAddress, nuint.Zero, MEM_RELEASE);
            }
        }
        finally
        {
            IsDisposed = true;
        }
    }

    public override unsafe string ToString()
    {
        nint start = _memoryPointer - _baseAddress;
        nint end = start + _length * sizeof(T);
        return $"GuardedMemoryManager<{typeof(T)}> {{ Page: {_pageSize}, Memory range: {start}..{end}, Total range: 0..{_totalSize} }}";
    }
}
