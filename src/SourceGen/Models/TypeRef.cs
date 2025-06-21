using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FlameCsv.SourceGen.Models;

// source: dotnet runtime (MIT license)

[DebuggerDisplay("Name = {Name}")]
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TypeRef : IComparable<TypeRef>
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
    /// TypeKind of the type.
    /// </summary>
    public TypeKind Kind { get; }

    /// <summary>
    /// Whether the type is ref-like, such as Span.
    /// </summary>
    public bool IsRefLike => (_config & Config.IsRefLike) != 0;

    /// <summary>
    /// Whether the type is a value type.
    /// </summary>
    /// <remarks>
    /// Not needed by the generator, but should be included for type equality (+ padding space in the struct).
    /// </remarks>
    public bool IsValueType => (_config & Config.IsValueType) != 0;

    /// <summary>
    /// Whether the type is abstract.
    /// </summary>
    public bool IsAbstract => (_config & Config.IsAbstract) != 0;

    /// <summary>
    /// If the type is an enum or a nullable enum.
    /// </summary>
    public bool IsEnumOrNullableEnum => (_config & Config.IsEnumOrNullableEnum) != 0;

    private readonly Config _config;

    public TypeRef(ITypeSymbol type)
    {
        Name = type.SpecialType switch
        {
            SpecialType.System_Char => "char",
            SpecialType.System_Byte => "byte",
            _ => type.Name,
        };

        FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        SpecialType = type.OriginalDefinition.SpecialType;
        Kind = type.TypeKind;

        _config =
            (type.IsRefLikeType ? Config.IsRefLike : 0)
            | (type.IsValueType ? Config.IsValueType : 0)
            | (type.IsAbstract ? Config.IsAbstract : 0)
            | (IsEnumOrNullableEnum(type) ? Config.IsEnumOrNullableEnum : 0);

        static bool IsEnumOrNullableEnum(ITypeSymbol type)
        {
            return type.TypeKind is TypeKind.Enum
                || type
                    is INamedTypeSymbol
                    {
                        OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                        TypeArguments: [{ TypeKind: TypeKind.Enum }]
                    };
        }
    }

    public override string ToString() => $"{{ TypeRef: {Name} ({Kind}) }}";

    public int CompareTo(TypeRef other) => StringComparer.Ordinal.Compare(FullyQualifiedName, other.FullyQualifiedName);

    public bool Equals(TypeRef other) => FullyQualifiedName == other.FullyQualifiedName;

    public override int GetHashCode() => FullyQualifiedName.GetHashCode();

    [Flags]
    private enum Config : byte
    {
        IsRefLike = 1 << 0,
        IsValueType = 1 << 1,
        IsAbstract = 1 << 2,
        IsEnumOrNullableEnum = 1 << 3,
    }
}
