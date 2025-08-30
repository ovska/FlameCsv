using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

// a stackalloc incurs buffer overrun cookie penalty
[InlineArray(32)]
internal struct Inline32<T>
    where T : unmanaged
{
    public T elem0;
}

[InlineArray(64)]
internal struct Inline64<T>
    where T : unmanaged
{
    public T elem0;
}
