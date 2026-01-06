using System.Runtime.CompilerServices;

namespace FlameCsv.Reading.Internal;

[InlineArray(64)]
internal struct Inline64<T>
    where T : unmanaged
{
    public T elem0;
}

[InlineArray(128)]
internal struct Inline128<T>
    where T : unmanaged
{
    public T elem0;
}
