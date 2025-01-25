using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen;

internal static class Extensions
{
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
            _ when GetLiteral(value) is { } les => les.ToFullString(),
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        static LiteralExpressionSyntax? GetLiteral(object? value)
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

    public static bool HasAttribute(this ISymbol symbol, INamedTypeSymbol attribute)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attribute))
                return true;
        }

        return false;
    }
}
