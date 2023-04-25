using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    public static ReadOnlyMemory<T> SafeCopy<T>(this ReadOnlyMemory<T> data)
    {
        if (data.IsEmpty)
            return data;

        // strings are immutable and safe to return as-is
        if (typeof(T) == typeof(char) &&
            MemoryMarshal.TryGetString((ReadOnlyMemory<char>)(object)data, out _, out _, out _))
        {
            return data;
        }

        return data.ToArray();
    }

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
        in CsvDialect<T> dialect)
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
            (dialect, memoryOwner.Memory),
            static (destination, state) =>
            {
                (CsvDialect<T> dialect, ReadOnlyMemory<T> memory) = state;
                var source = memory.Span;

                var newline = dialect.Newline.Span;

                destination.Fill('x');

                for (int i = 0; i < destination.Length; i++)
                {
                    T token = source[i];

                    if (token.Equals(dialect.Delimiter))
                    {
                        destination[i] = ',';
                    }
                    else if (token.Equals(dialect.Quote))
                    {
                        destination[i] = '"';
                    }
                    else if (dialect.Escape.HasValue && token.Equals(dialect.Escape.Value))
                    {
                        destination[i] = 'E';
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
