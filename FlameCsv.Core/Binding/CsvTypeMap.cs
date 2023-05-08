using FlameCsv.Runtime;

namespace FlameCsv.Binding;

/// <summary>
/// Provides compile-time mapping to parse <typeparamref name="TValue"/> records from CSV.
/// </summary>
/// <remarks>
/// Decorate a non-generic <see langword="partial"/> <see langword="class"/> with <see cref="CsvTypeMapAttribute{T, TValue}"/>
/// to generate the implementation.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Record type</typeparam>
public abstract partial class CsvTypeMap<T, TValue> where T : unmanaged, IEquatable<T>
{
    protected interface ITypeMapState
    {
        int Count { get; }
        bool TryParse(int index, ref TValue value, ReadOnlySpan<T> field);
    }

    /// <summary>
    /// Creates an instance of <typeparamref name="TValue"/> that is hydrated from CSV records.
    /// </summary>
    protected abstract TValue CreateInstance();

    /// <summary>
    /// Returns bindings for <typeparamref name="TValue"/> for the headers.
    /// </summary>
    protected abstract object BindMembers(
        ReadOnlySpan<string> headers,
        bool exposeContent,
        CsvReaderOptions<T> options);

    internal IMaterializer<T, TValue> GetMaterializer(in CsvReadingContext<T> context)
    {
        throw new NotImplementedException();
    }

    internal IMaterializer<T, TValue> GetMaterializer(ReadOnlySpan<string> headers, in CsvReadingContext<T> context)
    {
        return (IMaterializer<T, TValue>)BindMembers(headers, context.ExposeContent, context.Options);
    }
}
