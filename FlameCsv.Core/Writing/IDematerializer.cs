using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Writing;
    
public interface IDematerializer<T, in TValue> where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Formats <typeparamref name="TValue"/> into a CSV record, including the trailing newline.
    /// </summary>
    void Write(ICsvFieldWriter<T> writer, [AllowNull] TValue value);

    /// <summary>
    /// Writes a header if needed, including the trailing newline.
    /// </summary>
    void WriteHeader(ICsvFieldWriter<T> writer);
}
