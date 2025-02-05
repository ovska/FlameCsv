using System.Collections.Immutable;
using FlameCsv.SourceGen.Helpers;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen;

internal static class Extensions
{
    /// <summary>
    /// Returns an <see cref="EquatableArray{T}"/> of the unique string values.
    /// </summary>
    public static EquatableArray<string> TryToEquatableStringArray(this ImmutableArray<TypedConstant> values)
    {
        if (values.IsDefaultOrEmpty)
        {
            return [];
        }

        HashSet<string> builder = PooledSet<string>.Acquire();

        foreach (var value in values)
        {
            if (value.Value?.ToString() is { Length: > 0 } headerName)
            {
                builder.Add(headerName);
            }
        }

        return builder.ToEquatableArrayAndFree();
    }

    public static ImmutableArray<IMethodSymbol> GetInstanceConstructors(this ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
            ? namedType.InstanceConstructors
            : [..type.GetMembers(".ctor").OfType<IMethodSymbol>()]; // should be rare
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
            _ => GetLiteral(value)?.ToFullString() ?? value.ToString() ?? ""
        };

        static LiteralExpressionSyntax? GetLiteral(object value)
        {
            const SyntaxKind CharacterLiteral = SyntaxKind.CharacterLiteralExpression;
            const SyntaxKind StringLiteral = SyntaxKind.StringLiteralExpression;
            const SyntaxKind NumericLiteral = SyntaxKind.NumericLiteralExpression;

            return value switch
            {
                char c => SyntaxFactory.LiteralExpression(CharacterLiteral, SyntaxFactory.Literal(c)),
                string s => SyntaxFactory.LiteralExpression(StringLiteral, SyntaxFactory.Literal(s)),
                int i => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(i)),
                long l => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(l)),
                float f => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(f)),
                double d => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(d)),
                decimal d => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(d)),
                uint ui => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(ui)),
                ulong ul => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(ul)),
                short s => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(s)),
                ushort us => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(us)),
                byte b => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(b)),
                sbyte sb => SyntaxFactory.LiteralExpression(NumericLiteral, SyntaxFactory.Literal(sb)),
                _ => null,
            };
        }
    }
}
