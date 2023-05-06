using FlameCsv.Runtime;

namespace FlameCsv.Binding;

public abstract partial class CsvTypeMap<T, TValue> where T : unmanaged, IEquatable<T>
{
    protected delegate bool TryParseHandler(ref TValue value, ReadOnlySpan<T> data);

    protected abstract bool IgnoreUnparsable { get; }

    /// <summary>
    /// Creates an instance of <typeparamref name="TValue"/> that is hydrated from CSV records.
    /// </summary>
    protected abstract TValue CreateInstance();

    /// <summary>
    /// Binds header field with the specified name to a member of <typeparamref name="TValue"/>.
    /// </summary>
    /// <remarks>Source generated.</remarks>
    protected abstract TryParseHandler? BindMember(string name, ref BindingState state);

    /// <summary>
    /// Validates that all required fields have been read.
    /// </summary>
    protected abstract void ValidateFields(ICollection<string> headers, BindingState state);

    internal IMaterializer<T, TValue> GetMaterializer(in CsvReadingContext<T> context)
    {
        throw new NotImplementedException();
    }

    internal IMaterializer<T, TValue> GetMaterializer(ICollection<string> headers, in CsvReadingContext<T> context)
    {
        var handlers = new TryParseHandler?[headers.Count];
        int index = 0;

        BindingState state = new(in context);

        foreach (var header in headers)
        {
            handlers[index++] = BindMember(header, ref state);
        }

        if (state.Count == 0)
        {
            ThrowNoFieldsBound(headers, context.ExposeContent);
        }

        ValidateFields(headers, state);

        return new TypeMapMaterializer(CreateInstance, handlers);
    }
}
