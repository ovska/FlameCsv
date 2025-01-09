using System.Buffers;
using System.Text;
using System.Text.Unicode;

namespace FlameCsv.Utilities;

internal static class Utf8Util
{
    internal static bool SequenceEqual(
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right,
        StringComparison comparison)
    {
        if (comparison == StringComparison.Ordinal)
            return left.SequenceEqual(right);

        if (comparison == StringComparison.OrdinalIgnoreCase && Ascii.IsValid(left) && Ascii.IsValid(right))
        {
            return Ascii.EqualsIgnoreCase(left, right);
        }

        return SequenceEqualSlow(left, right, comparison);
    }

    public static bool SequenceEqualSlow(
        ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right,
        StringComparison comparison)
    {
        scoped Span<char> bleft = stackalloc char[256];
        scoped Span<char> bright = stackalloc char[256];

        while (true)
        {
            // ran out of data?
            if (left.IsEmpty)
                return right.IsEmpty;

            if (right.IsEmpty)
                break;

            OperationStatus status = Utf8.ToUtf16(
                left,
                bleft,
                out int bytesRead,
                out int charsWritten,
                replaceInvalidSequences: false);

            if (status == OperationStatus.InvalidData)
                break;

            ReadOnlySpan<char> leftchars = bleft[..charsWritten];
            left = left[bytesRead..];

            status = Utf8.ToUtf16(right, bright, out bytesRead, out charsWritten, replaceInvalidSequences: false);

            if (status == OperationStatus.InvalidData)
                break;

            ReadOnlySpan<char> rightchars = bright[..charsWritten];
            right = right[bytesRead..];

            if (!leftchars.Equals(rightchars, comparison))
                return false;
        }

        return false;
    }

    public static bool SequenceEqual(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<char> chars,
        StringComparison comparison)
    {
        if (bytes.IsEmpty)
            return chars.IsEmpty;

        if (comparison == StringComparison.Ordinal)
        {
            if (Ascii.IsValid(bytes))
                return Ascii.Equals(chars, bytes);
        }
        else if (comparison == StringComparison.OrdinalIgnoreCase)
        {
            if (Ascii.IsValid(bytes))
                return Ascii.EqualsIgnoreCase(chars, bytes);
        }

        if (Encoding.UTF8.GetMaxCharCount(bytes.Length) < chars.Length)
            return false;

        scoped Span<char> buffer = stackalloc char[256];

        while (true)
        {
            if (bytes.IsEmpty)
                return chars.IsEmpty;

            // Ran out of chars before bytes
            if (chars.IsEmpty)
                break;

            OperationStatus status = Utf8.ToUtf16(
                bytes,
                buffer,
                out int bytesRead,
                out int charsWritten,
                replaceInvalidSequences: false);

            if (status == OperationStatus.InvalidData)
                break;

            if (charsWritten > chars.Length)
                break;

            if (!chars[..charsWritten].Equals(buffer[..charsWritten], comparison))
                break;

            bytes = bytes.Slice(bytesRead);
            chars = chars.Slice(charsWritten);
        }

        return false;
    }
}
