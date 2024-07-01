using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen;

internal static class Extensions
{
    public static bool IsExplicitInterfaceImplementation(this IPropertySymbol propertySymbol)
    {
        foreach (var implementation in propertySymbol.ExplicitInterfaceImplementations)
        {
            if (SymbolEqualityComparer.Default.Equals(propertySymbol, implementation))
            {
                return true;
            }
        }

        return false;
    }

    public static ITypeSymbol UnwrapNullable(this ITypeSymbol type, out bool isNullable)
    {
        if (type is { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T })
        {
            isNullable = true;
            return ((INamedTypeSymbol)type).TypeArguments[0];
        }

        isNullable = false;
        return type;
    }

    public static bool IsEnumOrNullableEnum(this ITypeSymbol type)
    {
        return type.UnwrapNullable(out _) is { BaseType.SpecialType: SpecialType.System_Enum };
    }

    public static bool Inherits(this ITypeSymbol? type, ITypeSymbol baseClass)
    {
        while ((type = type?.BaseType) != null)
        {
            if (SymbolEqualityComparer.Default.Equals(type, baseClass))
            {
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<ISymbol> GetPublicMembersRecursive(this ITypeSymbol typeSymbol)
    {
        ITypeSymbol? current = typeSymbol;

        // keep track of properties to not duplicate them for interfaces
        HashSet<ISymbol> properties = new(SymbolEqualityComparer.Default);

        while (current is not null
            && current.SpecialType != SpecialType.System_Object
            && current.SpecialType != SpecialType.System_ValueType)
        {
            foreach (var member in current.GetMembers())
            {
                if (member.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected)
                    continue;

                if (member is IPropertySymbol prop)
                {
                    properties.Add(prop.OriginalDefinition);
                }

                yield return member;
            }

            current = current.BaseType;
        }

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected)
                    continue;

                if (member is IPropertySymbol prop)
                {
                    if (typeSymbol.FindImplementationForInterfaceMember(prop) is not { } impl ||
                        !properties.Add(impl.OriginalDefinition))
                    {
                        continue;
                    }
                }

                yield return member;
            }
        }
    }

    public static string ToStringLiteral(this string? value)
    {
        if (value is null)
            return "null";

        if (value == "")
            return "\"\"";

        return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value)).ToFullString();
    }

    public static string ToLiteral(this object? value)
    {
        return value switch
        {
            null => "default",
            bool b => b ? "true" : "false",
            _ when GetLES(value) is { } les => les.ToFullString(),
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        static LiteralExpressionSyntax? GetLES(object? value)
        {
            return value switch
            {
                char c => SyntaxFactory.LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(c)),
                string s => SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(s)),
                int i => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i)),
                long l => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(l)),
                float f => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(f)),
                double d => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(d)),
                decimal d => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(d)),
                uint ui => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(ui)),
                ulong ul => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(ul)),
                short s => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(s)),
                ushort us => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(us)),
                byte b => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(b)),
                sbyte sb => SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(sb)),
                _ => null,
            };
        }
    }

    public static bool ValidFor(this IFieldSymbol f, ref readonly TypeMapSymbol typeMap)
    {
        if (!f.CanBeReferencedByName ||
            f.IsStatic ||
            f.RefKind != RefKind.None ||
            f.HasAttribute(typeMap.Symbols.CsvHeaderIgnoreAttribute))
        {
            return false;
        }

        // either field must be writable, or we are generating writing code too
        return typeMap.Scope != BindingScope.Read || (!f.IsReadOnly && !f.IsConst);
    }

    public static bool ValidFor(this IPropertySymbol p, ref readonly TypeMapSymbol typeMap)
    {
        if (!p.CanBeReferencedByName ||
            p.IsStatic ||
            p.IsIndexer ||
            p.RefKind != RefKind.None ||
            p.HasAttribute(typeMap.Symbols.CsvHeaderIgnoreAttribute))
        {
            return false;
        }

        return typeMap.Scope switch
        {
            BindingScope.Read => !p.IsReadOnly, // only reading code, must be writable
            BindingScope.Write => !p.IsWriteOnly, // only writing code, must be readable
            _ => true,
        };
    }

    public static bool HasAttribute(this ISymbol symbol, INamedTypeSymbol attribute)
    {
        foreach (var _attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(_attribute.AttributeClass, attribute))
                return true;
        }

        return false;
    }
}
