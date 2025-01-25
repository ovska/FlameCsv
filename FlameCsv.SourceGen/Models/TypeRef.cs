using System.Diagnostics;

namespace FlameCsv.SourceGen.Models;

// source: dotnet runtime (MIT license)

[DebuggerDisplay("Name = {Name}")]
public sealed class TypeRef : IEquatable<TypeRef>, IComparable<TypeRef>
{
    public TypeRef(ITypeSymbol type)
    {
        Name = type.SpecialType switch
        {
            SpecialType.System_Char => "char",
            SpecialType.System_Byte => "byte",
            _ => type.Name
        };

        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        IsValueType = type.IsValueType;
        TypeKind = type.TypeKind;
        SpecialType = type.OriginalDefinition.SpecialType;
        IsRefLike = type.IsRefLikeType;

        IsEnumOrNullableEnum
            = (SpecialType is SpecialType.System_Nullable_T ? ((INamedTypeSymbol)type).TypeArguments[0] : type) is
            {
                BaseType.SpecialType: SpecialType.System_Enum
            };
    }

    public string Name { get; }

    /// <summary>
    /// Fully qualified assembly name, prefixed with "global::", e.g., global::System.Numerics.BigInteger.
    /// </summary>
    public string FullyQualifiedName { get; }

    public bool IsValueType { get; }
    public TypeKind TypeKind { get; }
    public SpecialType SpecialType { get; }

    public bool IsRefLike { get; }
    public bool IsEnumOrNullableEnum { get; }

    public bool CanBeNull => !IsValueType || SpecialType is SpecialType.System_Nullable_T;

    public int CompareTo(TypeRef other) => StringComparer.Ordinal.Compare(FullyQualifiedName, other.FullyQualifiedName);
    public bool Equals(TypeRef? other) => FullyQualifiedName == other?.FullyQualifiedName;
    public override bool Equals(object? obj) => Equals(obj as TypeRef);
    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
}
