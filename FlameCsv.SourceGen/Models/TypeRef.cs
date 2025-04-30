using System.Diagnostics;

namespace FlameCsv.SourceGen.Models;

// source: dotnet runtime (MIT license)

[DebuggerDisplay("Name = {Name}")]
public readonly record struct TypeRef : IEquatable<TypeRef>, IComparable<TypeRef>
{
    /// <summary>
    /// Name of the type, e.g., int, string, System.Numerics.BigInteger.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Fully qualified assembly name, prefixed with "global::", e.g., global::System.Numerics.BigInteger.
    /// </summary>
    public string FullyQualifiedName { get; }

    /// <summary>
    /// Special type of the underlying type symbol.
    /// </summary>
    public SpecialType SpecialType { get; }

    /// <summary>
    /// Whether the type is ref-like, such as Span.
    /// </summary>
    public bool IsRefLike { get; }

    /// <summary>
    /// Whether the type is a value type.
    /// </summary>
    public bool IsValueType { get; }

    /// <summary>
    /// Whether the type is abstract.
    /// </summary>
    public bool IsAbstract { get; }

    /// <summary>
    /// TypeKind of the type.
    /// </summary>
    public TypeKind Kind { get; }

    /// <summary>
    /// If the type is an enum or a nullable enum.
    /// </summary>
    public bool IsEnumOrNullableEnum { get; }

    public TypeRef(ITypeSymbol type)
    {
        Name = type.SpecialType switch
        {
            SpecialType.System_Char => "char",
            SpecialType.System_Byte => "byte",
            _ => type.Name
        };

        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        SpecialType = type.OriginalDefinition.SpecialType;
        IsRefLike = type.IsRefLikeType;
        IsValueType = type.IsValueType;
        IsAbstract = type.IsAbstract;
        Kind = type.TypeKind;
        IsEnumOrNullableEnum = type.TypeKind is TypeKind.Enum ||
        (
            (type.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T) &&
            ((INamedTypeSymbol)type).TypeArguments[0].TypeKind is TypeKind.Enum
        );
    }

    public override string ToString() => $"{{ TypeRef: {Name} }}";

    public int CompareTo(TypeRef other) => StringComparer.Ordinal.Compare(FullyQualifiedName, other.FullyQualifiedName);
    public bool Equals(TypeRef other) => FullyQualifiedName == other.FullyQualifiedName;
    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
}
