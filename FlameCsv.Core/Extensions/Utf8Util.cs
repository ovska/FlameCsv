using System.Buffers;
using System.Text;
using System.Text.Unicode;

namespace FlameCsv.Extensions;

internal static class Utf8Util
{
    internal static bool SequenceEqual(
        ReadOnlySpan<byte> bytes,
        ReadOnlySpan<char> chars,
        StringComparison comparison)
    {
        if (!bytes.IsEmpty && Encoding.UTF8.GetMaxCharCount(bytes.Length) < chars.Length)
            return false;

        scoped Span<char> buffer = stackalloc char[Math.Max(chars.Length, 128)];

        while (true)
        {
            if (bytes.IsEmpty)
                return chars.IsEmpty;

            // Ran out of chars before bytes
            if (chars.IsEmpty)
                break;

            var status = Utf8.ToUtf16(bytes, buffer, out int bytesRead, out int charsWritten, replaceInvalidSequences: false);

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
