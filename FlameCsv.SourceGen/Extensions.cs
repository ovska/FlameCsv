using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen;

internal static class Extensions
{
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

    public static TTo FindValueOrDefault<TFrom, TTo>(
        this SyntaxList<TFrom> syntaxList,
        Func<TFrom, bool> predicate,
        Func<TFrom, TTo> valueSelector)
        where TFrom : SyntaxNode
    {
        foreach (var node in syntaxList)
        {
            if (predicate(node))
            {
                return valueSelector(node);
            }
        }

        return default!;
    }

    public static T FindValueOrDefault<T>(this ImmutableArray<T> array, Func<T, bool> predicate)
    {
        foreach (var item in array)
        {
            if (predicate(item))
            {
                return item;
            }
        }

        return default!;
    }

    public static IEnumerable<ISymbol> GetPublicMembersRecursive(this ITypeSymbol typeSymbol)
    {
        ITypeSymbol? current = typeSymbol;

        HashSet<ISymbol> properties = new(SymbolEqualityComparer.Default);

        while (current is not null)
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
            return "default(string)";

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

    public static bool IsSerializerWritable(this IFieldSymbol f, in KnownSymbols knownSymbols)
    {
        return !f.IsStatic
            && f.CanBeReferencedByName
            && !f.IsReadOnly
            && !f.HasAttribute(knownSymbols.CsvHeaderIgnoreAttribute);
    }

    public static bool IsSerializerWritable(this IPropertySymbol p, in KnownSymbols knownSymbols)
    {
        return !p.IsStatic
            && !p.IsReadOnly
            && !p.IsIndexer
            && p.CanBeReferencedByName
            && !p.HasAttribute(knownSymbols.CsvHeaderIgnoreAttribute);
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
