using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace FlameCsv.Binding;

public abstract class CsvBinding<T> :
    CsvBinding,
    IEquatable<CsvBinding>,
    IEquatable<CsvBinding<T>>
{
#pragma warning disable RCS1158 // Static member in generic type should use a type parameter.
    internal static readonly bool _isInvalid = typeof(T).IsInterface;

    internal static void ThrowIfInvalid()
    {
        if (_isInvalid)
            throw new NotSupportedException("Interface binding is not supported");
    }
#pragma warning restore RCS1158 // Static member in generic type should use a type parameter.

    protected internal CsvBinding(int index) : base(index)
    {
    }

    /// <summary>
    /// Returns the type of the binding's target (property/field/parameter type).
    /// For ignored fields, returns <c>typeof(object)</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public abstract Type Type { get; }

    /// <summary>
    /// Returns the custom attributes on the binding, or empty if not applicable (e.g. ignored column).
    /// </summary>
    protected abstract ReadOnlySpan<object> Attributes { get; }

    public bool TryGetAttribute<TAttribute>([NotNullWhen(true)] out TAttribute? attribute)
        where TAttribute : Attribute
    {
        foreach (var attr in Attributes)
        {
            if (attr is TAttribute tAttr)
            {
                attribute = tAttr;
                return true;
            }
        }

        attribute = default;
        return false;
    }

    /// <inheritdoc/>
    public bool Equals(CsvBinding<T>? other) => Equals(other as CsvBinding);

    /// <inheritdoc/>
    public bool Equals(CsvBinding? other)
    {
        return other is not null && Index == other.Index && TargetEquals(other);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CsvBinding);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Index, Sentinel);

    /// <inheritdoc/>
    public static bool operator ==(CsvBinding<T> left, CsvBinding<T> right) => left.Equals(right);
    /// <inheritdoc/>
    public static bool operator !=(CsvBinding<T> left, CsvBinding<T> right) => !(left == right);

    /// <summary>Returns the column index and member name.</summary>
    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        var sb = new StringBuilder(64);
        sb.Append($"{{ [{nameof(CsvBinding<T>)}] Index: {Index}, ");
        PrintDetails(sb);
        sb.Append(" }");
        return sb.ToString();
    }

    protected abstract void PrintDetails(StringBuilder sb);
}
