using System.Collections.Immutable;
using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen.Utilities;

internal static class SymbolExtensions
{
    public static ITypeSymbol? GetMemberType(this ISymbol? symbol)
    {
        return symbol switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            IParameterSymbol parameter => parameter.Type,
            _ => null,
        };
    }

    public static bool TryGetFlameCsvAttribute(
        this AttributeData attributeData,
        [NotNullWhen(true)] out INamedTypeSymbol? attribute
    )
    {
        if (
            attributeData.AttributeClass is
            { ContainingNamespace: { Name: "Attributes", ContainingNamespace.Name: "FlameCsv" } } attrSymbol
        )
        {
            attribute = attrSymbol;
            return true;
        }

        attribute = null;
        return false;
    }

    public static Location? GetLocation(this AttributeData attribute)
    {
        return attribute.ApplicationSyntaxReference?.SyntaxTree.GetLocation(attribute.ApplicationSyntaxReference.Span);
    }

    public static ImmutableArray<IMethodSymbol> GetInstanceConstructors(this ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
            ? namedType.InstanceConstructors
            : [.. type.GetMembers(".ctor").OfType<IMethodSymbol>()]; // should be rare
    }

    public static bool IsNullable(this ITypeSymbol type, [NotNullWhen(true)] out ITypeSymbol? baseType)
    {
        if (type is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            baseType = ((INamedTypeSymbol)type).TypeArguments[0];
            return true;
        }

        baseType = null;
        return false;
    }
    
    public static bool IsByte(in this TypeRef typeRef) => typeRef.SpecialType == SpecialType.System_Byte;
}
