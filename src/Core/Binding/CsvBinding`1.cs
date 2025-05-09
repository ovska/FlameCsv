using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Attributes;
using FlameCsv.Utilities;

namespace FlameCsv.Binding;

/// <summary>
/// A binding between a CSV field and a property, field, or parameter.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public abstract class CsvBinding<T> : CsvBinding, IEquatable<CsvBinding>, IEquatable<CsvBinding<T>>
{
    private protected CsvBinding(int index, string? header)
        : base(index, header) { }

    /// <inheritdoc />
    public override Type Type => typeof(T);

    /// <summary>
    /// Returns the custom attributes on the binding, or empty if not applicable (e.g., ignored field).
    /// </summary>
    protected abstract ReadOnlySpan<object> Attributes { get; }

    /// <inheritdoc/>
    public bool Equals(CsvBinding<T>? other) => Equals(other as CsvBinding);

    /// <inheritdoc/>
    public bool Equals(CsvBinding? other) => other is not null && Index == other.Index && TargetEquals(other);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CsvBinding);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Index, Sentinel);

    /// <summary>Returns the field index and member name.</summary>
    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        var vsb = new ValueStringBuilder(stackalloc char[64]);

        vsb.Append("{ CsvBinding<");
        vsb.Append(typeof(T).Name);
        vsb.Append("> Index: ");
        vsb.AppendFormatted(Index);

        PrintDetails(ref vsb);

        if (!string.IsNullOrEmpty(Header))
        {
            vsb.Append(", Header: \"");
            vsb.Append(Header);
            vsb.Append('"');
        }

        vsb.Append(" }");
        return vsb.ToString();
    }

    /// <summary>
    /// Prints details for debugging to the builder.
    /// </summary>
    private protected abstract void PrintDetails(ref ValueStringBuilder vsb);

    /// <summary>
    /// Display name for debugging.
    /// </summary>
    protected internal abstract string DisplayName { get; }

    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    internal CsvConverter<TToken, TResult> ResolveConverter<TToken, TResult>(CsvOptions<TToken> options)
        where TToken : unmanaged, IBinaryInteger<TToken>
    {
        foreach (var attribute in Attributes)
        {
            if (
                attribute is CsvConverterAttribute @override
                && @override.TryCreateConverter(Type, options, out var converter)
            )
            {
                return (CsvConverter<TToken, TResult>)converter;
            }
        }

        return options.GetConverter<TResult>();
    }

    /// <summary>
    /// Comparer that compares bindings by their target property, field, or parameter.
    /// </summary>
    public static IEqualityComparer<CsvBinding<T>> TargetComparer { get; } = new TargetEqualityComparer();

    private sealed class TargetEqualityComparer : IEqualityComparer<CsvBinding<T>>
    {
        public bool Equals(CsvBinding<T>? x, CsvBinding<T>? y)
        {
            if (x is null)
            {
                return y is null;
            }

            return x.TargetEquals(y);
        }

        public int GetHashCode(CsvBinding<T> obj)
        {
            return obj.Sentinel.GetHashCode();
        }
    }
}
