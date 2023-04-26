using System.Buffers;
using FlameCsv.Writers;

namespace FlameCsv;

public readonly struct CsvContextOverride
{

}

public interface ICsvOptions<T> where T : unmanaged, IEquatable<T>
{
    bool AllowContentInExceptions { get; }

    ArrayPool<T>? ArrayPool { get; }
    bool HasHeader { get; }
    IDictionary<Type, string?> NullTokens { get; }
    CsvExceptionHandler<T>? ExceptionHandler { get; }

    string GetAsString(ReadOnlySpan<T> field);
    ReadOnlyMemory<T> GetNullToken(Type resultType);
}

public interface ICsvWriterOptions<T> where T : unmanaged, IEquatable<T>
{
    bool IsReadOnly { get; }
    bool MakeReadOnly();

    CsvFieldQuoting FieldQuoting { get; }
}

public interface ICsvReaderOptions<T> where T : unmanaged, IEquatable<T>
{
    bool IsReadOnly { get; }
    bool MakeReadOnly();

    StringComparison Comparison { get; }
    CsvCallback<T, bool>? ShouldSkipRow { get; }
    bool ValidateFieldCount { get; }
}
