using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace FlameCsv.Binding;

public abstract class CsvBinding<T> :
    CsvBinding,
    IEquatable<CsvBinding>,
    IEquatable<CsvBinding<T>>
{
    protected internal CsvBinding(int index, string? header) : base(index, header)
    {
    }

    /// <summary>
    /// Returns the type of the binding's target (property/field/parameter type).
    /// For ignored fields, returns <c>typeof(object)</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public abstract Type Type { get; }

    /// <summary>
    /// Returns the custom attributes on the binding, or empty if not applicable (e.g. ignored field).
    /// </summary>
    protected internal abstract ReadOnlySpan<object> Attributes { get; }

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
    public bool Equals(CsvBinding? other) => other is not null && Index == other.Index && TargetEquals(other);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as CsvBinding);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Index, Sentinel);

    /// <inheritdoc/>
    public static bool operator ==(CsvBinding<T> left, CsvBinding<T> right)
    {
        return left is null ? right is null : left.Equals(right as CsvBinding);
    }

    /// <inheritdoc/>
    public static bool operator !=(CsvBinding<T> left, CsvBinding<T> right) => !(left == right);

    /// <summary>Returns the field index and member name.</summary>
    [ExcludeFromCodeCoverage]
    public override string ToString()
    {
        var sb = new StringBuilder(64);
        sb.Append(CultureInfo.InvariantCulture, $"{{ [{nameof(CsvBinding<T>)}] Index: {Index}, ");
        PrintDetails(sb);

        if (!string.IsNullOrEmpty(Header))
            sb.Append($", Header: \"{Header}\"");

        sb.Append(" }");
        return sb.ToString();
    }

    protected abstract void PrintDetails(StringBuilder sb);
}
