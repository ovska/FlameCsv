using System.Globalization;
using System.Text;
using CommunityToolkit.Diagnostics;
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

    public static T CreateInstance<T>(this Type type, params object?[] parameters) where T : class
    {
        try
        {
            var instance = Activator.CreateInstance(type, parameters)
                ?? throw new InvalidOperationException($"Instance of {type.ToTypeString()} could not be created");
            return (T)instance;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Could not create {0} from type {1} and {2} constructor parameters: [{3}]",
                    typeof(T).ToTypeString(),
                    type.ToTypeString(),
                    parameters.Length,
                    string.Join(", ", parameters.Select(o => o?.GetType().ToTypeString() ?? "<null>"))),
                innerException: e);
        }
    }

    public static string AsPrintableString<T>(
        this ReadOnlySpan<T> value,
        bool exposeContent,
        in CsvDialect<T> tokens)
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
                (CsvDialect<T> tokens, ReadOnlyMemory<T> memory) = state;
                var source = memory.Span;

                var whitespace = tokens.Whitespace.Span;
                var newline = tokens.Newline.Span;

                destination.Fill('x');

                for (int i = 0; i < destination.Length; i++)
                {
                    T token = source[i];

                    if (token.Equals(tokens.Delimiter))
                    {
                        destination[i] = ',';
                    }
                    else if (token.Equals(tokens.Quote))
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
