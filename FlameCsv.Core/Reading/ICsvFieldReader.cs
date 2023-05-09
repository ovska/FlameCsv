using System.Diagnostics.CodeAnalysis;
using FlameCsv.Parsers;

namespace FlameCsv.Reading;

public interface ICsvFieldReader<T> where T : unmanaged, IEquatable<T>
{
    bool TryReadNext(out ReadOnlyMemory<T> field);
    void TryEnsureFieldCount(int fieldCount);
    void EnsureFullyConsumed(int fieldCount);
    [DoesNotReturn] void ThrowParseFailed(ReadOnlyMemory<T> field, ICsvParser<T>? parser);
}
