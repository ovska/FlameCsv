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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCRLF(this CsvNewline newline)
    {
        return newline is CsvNewline.CRLF || (newline is CsvNewline.Platform && Environment.NewLine == "\r\n");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetTokens<T>(this CsvNewline newline, out T first, out T second)
        where T : IBinaryInteger<T>
    {
        second = T.CreateTruncating('\n');

        if (newline.IsCRLF())
        {
            first = T.CreateTruncating('\r');
            return 2;
        }

        first = T.CreateTruncating('\n');
        return 1;
    }

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

    public static string AsPrintableString<T>(this Span<T> value)
        where T : unmanaged, IBinaryInteger<T> => AsPrintableString((ReadOnlySpan<T>)value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string AsPrintableString<T>(this ReadOnlySpan<T> value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(char))
        {
            return value.ToString();
        }

        if (typeof(T) == typeof(byte))
        {
            try
            {
                return Encoding.UTF8.GetString(value.Cast<T, byte>());
            }
            catch
            {
                // ignored
            }
        }

        var sb = new ValueStringBuilder(stackalloc char[128]);
        sb.Append('[');

        foreach (var item in value)
        {
            sb.AppendFormatted(item);
            sb.Append(",");
        }

        sb.Length--;
        sb.Append(']');
        return sb.ToString();
    }

    public static ReadOnlySpan<T> AsSpanUnsafe<T>(this ArraySegment<T> segment)
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(segment.Array!), segment.Offset),
            segment.Count
        );
    }

    public static bool SequenceEquals<T>(in this ReadOnlySequence<T> sequence, ReadOnlySpan<T> other)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (sequence.IsSingleSegment)
        {
            return sequence.FirstSpan.SequenceEqual(other);
        }

        if (sequence.Length != other.Length)
            return false;

        foreach (var memory in sequence)
        {
            if (!other.StartsWith(memory.Span))
                return false;

            other = other.Slice(memory.Length);
        }

        return true;
    }

    public static T CreateInstance<T>([DAM(Messages.Ctors)] this Type type, params object?[] parameters)
        where T : class
    {
        try
        {
            var instance =
                Activator.CreateInstance(type, parameters)
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
                    string.Join(", ", parameters.Select(o => o?.GetType().FullName ?? "<null>"))
                ),
                innerException: e
            );
        }
    }
}
