using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    /// <summary>
    /// Returns the array, or the shared <see cref="Array.Empty{T}"/> if it is empty.
    /// </summary>
    public static T[] ForCache<T>(this T[] array)
        => array.Length != 0 ? array : Array.Empty<T>();

    public static string AsPrintableString<T>(
        this ReadOnlySpan<T> value,
        bool exposeContent,
        in CsvTokens<T> tokens)
        where T : unmanaged, IEquatable<T>
    {
        string? content =
            !exposeContent ? null :
            typeof(T) == typeof(char) ? value.ToString() :
            typeof(T) == typeof(byte) ? Encoding.UTF8.GetString(value.Cast<T, byte>()) :
            null;

        using var memoryOwner = MemoryOwner<T>.Allocate(value.Length);
        value.CopyTo(memoryOwner.Memory.Span);

        string structure = string.Create(
            value.Length,
            (tokens, memoryOwner.Memory),
            static (destination, state) =>
            {
                (CsvTokens<T> tokens, ReadOnlyMemory<T> memory) = state;
                var source = memory.Span;

                var whitespace = tokens.Whitespace.Span;
                var newline = tokens.NewLine.Span;

                destination.Fill('x');

                for (int i = 0; i < destination.Length; i++)
                {
                    T token = source[i];

                    if (token.Equals(tokens.Delimiter))
                    {
                        destination[i] = ',';
                    }
                    else if (token.Equals(tokens.StringDelimiter))
                    {
                        destination[i] = '"';
                    }
                    else if (whitespace.Contains(token))
                    {
                        destination[i] = '_';
                    }
                    else if (newline.Contains(token))
                    {
                        destination[i] = 'N';
                    }
                }
            });

        if (content is null)
            return $"Data structure: [{structure}]";

        return $"Content: [{content}], data structure: [{structure}]";
    }
}
