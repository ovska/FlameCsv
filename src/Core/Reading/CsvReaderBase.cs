using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.IO.Internal;

namespace FlameCsv.Reading;

/// <summary>
/// Base class for CSV readers. This class is not intended to be used directly.
/// </summary>
public abstract class CsvReaderBase<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Current options instance.
    /// </summary>
    public CsvOptions<T> Options { get; }

    private protected CsvReaderBase(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        _dialect = new Dialect<T>(options);
        _unescapeAllocator = new MemoryPoolAllocator<T>(options.Allocator);
    }

    internal readonly Dialect<T> _dialect;

    internal readonly Allocator<T> _unescapeAllocator;
    private protected EnumeratorStack _stackMemory; // don't make me readonly!

    /// <summary>
    /// Returns a buffer that can be used for unescaping field data.
    /// The buffer is not valid after disposing the reader.
    /// </summary>
    internal protected Span<T> GetUnescapeBuffer(int length)
    {
        int stackLength = EnumeratorStack.Length / Unsafe.SizeOf<T>();

        // allocate a new buffer if the requested length is larger than the stack buffer
        if (length > stackLength)
        {
            return _unescapeAllocator.GetSpan(length);
        }

        return MemoryMarshal.Cast<byte, T>((Span<byte>)_stackMemory);
    }
}
