using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Utilities;

// ReSharper disable UnusedMember.Global

namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    public static string JoinValues(ReadOnlySpan<string> values)
    {
        // should never happen
        if (values.IsEmpty)
            return "";

        var sb = new ValueStringBuilder(stackalloc char[128]);

        sb.Append('[');

        foreach (var value in values)
        {
            sb.Append('"');
            sb.Append(value);
            sb.Append("\", ");
        }

        sb.Length -= 2;
        sb.Append(']');

        return sb.ToString();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string AsPrintableString<T>(this ReadOnlySpan<T> value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            try
            {
                return Encoding.UTF8.GetString(value.Cast<T, byte>());
            }
            catch
            {
                return $"[{string.Join(',', value.ToArray())}]";
            }
        }

        return value.ToString();
    }

    public static bool SequenceEquals<T>(in this ReadOnlySequence<T> sequence, ReadOnlySpan<T> other)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (sequence.Length != other.Length)
            return false;

        if (sequence.IsSingleSegment)
        {
            return sequence.FirstSpan.SequenceEqual(other);
        }

        foreach (var memory in sequence)
        {
            if (!other.StartsWith(memory.Span))
                return false;

            other = other.Slice(memory.Length);
        }

        return true;
    }

    public static ReadOnlyMemory<T> SafeCopy<T>(this ReadOnlyMemory<T> data)
    {
        if (data.IsEmpty)
            return data;

        // strings are immutable and safe to return as-is
        if (typeof(T) == typeof(char)
            && MemoryMarshal.TryGetString((ReadOnlyMemory<char>)(object)data, out _, out _, out _))
        {
            return data;
        }

        return data.ToArray();
    }

    public static T CreateInstance<T>([DAM(Messages.Ctors)] this Type type, params object?[] parameters) where T : class
    {
        try
        {
            var instance = Activator.CreateInstance(type, parameters)
                ?? throw new InvalidOperationException($"Instance of {type.FullName} could not be created");
            return (T)instance;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Could not create {0} from type {1} and {2} constructor parameters: [{3}]",
                    typeof(T).FullName,
                    type.FullName,
                    parameters.Length,
                    string.Join(", ", parameters.Select(o => o?.GetType().FullName ?? "<null>"))),
                innerException: e);
        }
    }
}
