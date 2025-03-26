using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Numerics;
using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Models;

namespace FlameCsv.SourceGen;

internal static class Extensions
{
    public static Location? GetLocation(this AttributeData attribute)
    {
        return attribute.ApplicationSyntaxReference?.SyntaxTree.GetLocation(attribute.ApplicationSyntaxReference.Span);
    }

    public static object? GetNamedArgument(this AttributeData attribute, string argumentName)
    {
        foreach (var kvp in attribute.NamedArguments)
        {
            if (kvp.Key == argumentName)
            {
                return kvp.Value.Value;
            }
        }

        return null;
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

    public static string ToCharLiteral(this char value)
    {
        if (value < 128 && char.IsLetterOrDigit(value))
            return $"'{value}'";

        return SyntaxFactory
            .LiteralExpression(SyntaxKind.CharacterLiteralExpression, SyntaxFactory.Literal(value))
            .ToFullString();
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

    public static bool IsAsciiLetter(this char c) => (c | 0x20) is >= 'a' and <= 'z';

    public static bool IsAscii(this string? value)
    {
        if (value is null || value.Length == 0) return true;

        ref char first = ref MemoryMarshal.GetReference(value.AsSpan());

        nint index = 0;
        nint remaining = value.Length;

        if (Vector.IsHardwareAccelerated && remaining >= Vector<ushort>.Count)
        {
            var needle = new Vector<ushort>(0x80);

            do
            {
                var mask = Unsafe.ReadUnaligned<Vector<ushort>>(
                    ref Unsafe.As<char, byte>(ref Unsafe.Add(ref first, index)));

                if (Vector.GreaterThanAny(mask, needle))
                {
                    return false;
                }

                index += Vector<ushort>.Count;
                remaining -= Vector<ushort>.Count;
            } while (remaining >= Vector<ushort>.Count);
        }

        while (remaining >= 4)
        {
            ulong mask = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref first, index)));

            // Check if any high bytes are non-zero
            if ((mask & 0xFF80_FF80_FF80_FF80) != 0)
            {
                return false;
            }

            index += 4;
            remaining -= 4;
        }

        while (remaining > 0)
        {
            if (Unsafe.Add(ref first, index) > 0x7F)
            {
                return false;
            }

            index++;
            remaining--;
        }

        return true;
    }

    public static IEnumerable<T> DistinctBy<T, TValue>(this IEnumerable<T> values, Func<T, TValue> selector)
        where TValue : IEquatable<TValue>
    {
        HashSet<TValue> set = PooledSet<TValue>.Acquire();

        try
        {
            foreach (var value in values)
            {
                if (set.Add(selector(value)))
                {
                    yield return value;
                }
            }
        }
        finally
        {
            PooledSet<TValue>.Release(set);
        }
    }

    public static bool IsByte(this TypeRef typeRef) => typeRef.SpecialType == SpecialType.System_Byte;
}
