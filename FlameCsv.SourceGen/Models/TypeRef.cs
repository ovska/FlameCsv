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

        if (type.OriginalDefinition.SpecialType is SpecialType.System_Nullable_T)
        {
            UnderlyingNullableType = new TypeRef(((INamedTypeSymbol)type).TypeArguments[0]);
        }
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
    public TypeRef? UnderlyingNullableType { get; }

    public bool IsEnum => SpecialType is SpecialType.System_Enum;
    public bool CanBeNull => !IsValueType || SpecialType is SpecialType.System_Nullable_T;

    public int CompareTo(TypeRef other) => StringComparer.Ordinal.Compare(FullyQualifiedName, other.FullyQualifiedName);
    public bool Equals(TypeRef? other) => FullyQualifiedName == other?.FullyQualifiedName;
    public override bool Equals(object? obj) => Equals(obj as TypeRef);
    public override int GetHashCode() => FullyQualifiedName.GetHashCode();
}
