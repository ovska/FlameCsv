using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen.Utilities;

internal static class StringExtensions
{
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
            null => "default", // null ref types and default/new() structs have this value
            bool b => b ? "true" : "false",
            string s => ToStringLiteral(s),
            _ => GetLiteral(value)?.ToFullString() ?? value.ToString() ?? "",
        };

        static LiteralExpressionSyntax? GetLiteral(object value)
        {
            const SyntaxKind characterLiteral = SyntaxKind.CharacterLiteralExpression;
            const SyntaxKind stringLiteral = SyntaxKind.StringLiteralExpression;
            const SyntaxKind numericLiteral = SyntaxKind.NumericLiteralExpression;

            return value switch
            {
                char c => SyntaxFactory.LiteralExpression(characterLiteral, SyntaxFactory.Literal(c)),
                string s => SyntaxFactory.LiteralExpression(stringLiteral, SyntaxFactory.Literal(s)),
                int i => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(i)),
                long l => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(l)),
                float f => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(f)),
                double d => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(d)),
                decimal d => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(d)),
                uint ui => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(ui)),
                ulong ul => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(ul)),
                short s => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(s)),
                ushort us => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(us)),
                byte b => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(b)),
                sbyte sb => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(sb)),
                nint n => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(n)),
                nuint nu => SyntaxFactory.LiteralExpression(numericLiteral, SyntaxFactory.Literal(nu)),
                _ => null, // enums show up as their numeric value so no need to handle them
            };
        }
    }

    public static bool IsAsciiLetter(this char c) => (c | 0x20) is >= 'a' and <= 'z';

    public static bool IsAsciiNumeric(this char c) =>
        c switch
        {
            >= '0' and <= '9' => true,
            '-' or '+' => true,
            _ => false,
        };

    public static bool ContainsSurrogates(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        ref char first = ref MemoryMarshal.GetReference(value.AsSpan());

        nint index = 0;
        nint remaining = value.Length;

        while (remaining >= 4)
        {
            if (
                char.IsSurrogate(Unsafe.Add(ref first, index))
                || char.IsSurrogate(Unsafe.Add(ref first, index + 1))
                || char.IsSurrogate(Unsafe.Add(ref first, index + 2))
                || char.IsSurrogate(Unsafe.Add(ref first, index + 3))
            )
            {
                return true;
            }

            index += 4;
            remaining -= 4;
        }

        while (remaining > 0)
        {
            if (char.IsSurrogate(Unsafe.Add(ref first, index)))
            {
                return true;
            }

            index++;
            remaining--;
        }

        return false;
    }

    /// <summary>
    /// Returns true if the string contains only ASCII characters (0x00 to 0x7F), or is null or empty.
    /// </summary>
    public static bool IsAscii(this string? value)
    {
        ReadOnlySpan<char> span = value.AsSpan();

        if (span.IsEmpty)
        {
            return true;
        }

        ref char first = ref MemoryMarshal.GetReference(value.AsSpan());

        nint index = 0;
        nint remaining = span.Length;

        if (Vector.IsHardwareAccelerated && remaining >= Vector<ushort>.Count)
        {
            var needle = new Vector<ushort>(0x80);

            do
            {
                var mask = Unsafe.ReadUnaligned<Vector<ushort>>(
                    ref Unsafe.As<char, byte>(ref Unsafe.Add(ref first, index))
                );

                if (Vector.GreaterThanOrEqualAny(mask, needle))
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

    public static bool IsCaseAgnostic(this string value)
    {
        return value.ToLowerInvariant() == value.ToUpperInvariant();
    }
}
