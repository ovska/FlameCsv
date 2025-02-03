using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen;

internal static class Extensions
{
    public static EquatableArray<string> TryToEquatableStringArray(this ImmutableArray<TypedConstant> values)
    {
        if (values.IsDefaultOrEmpty)
        {
            return [];
        }

        var builder = ImmutableArray.CreateBuilder<string>(values.Length);

        foreach (var value in values)
        {
            if (value.Value?.ToString() is { Length: > 0 } headerName)
            {
                builder.Add(headerName);
            }
        }

        return builder.ToImmutable();
    }

    public static ImmutableArray<IMethodSymbol> GetInstanceConstructors(this ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
            ? namedType.InstanceConstructors
            : [..type.GetMembers(".ctor").OfType<IMethodSymbol>()];
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

    public static string ToStringLiteral(this string? value)
    {
        if (value is null)
            return "null";

        if (value == "")
            return "\"\"";

        return SyntaxFactory
            .LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(value))
            .ToFullString();
    }

    public static string ToLiteral(this object? value)
    {
        return value switch
        {
            null => "default",
            bool b => b ? "true" : "false",
            string s => ToStringLiteral(s),
            _ when GetLiteral(value) is { } les => les.ToFullString(),
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

        static LiteralExpressionSyntax? GetLiteral(object? value)
        {
            // @formatter:off
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
            // @formatter:on
        }
    }
}
