using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCRLF(this CsvNewline newline)
    {
        // hoisting the check to a local allows JIT to "see" the constant and eliminate one jump instruction
        bool isWindows = Environment.NewLine == "\r\n";
        return newline is CsvNewline.CRLF || (newline is CsvNewline.Platform && isWindows);
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

    public static void Rethrow(this Exception ex)
    {
        if (ex is not null)
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    public static string AsPrintableString<T>(this Span<T> value)
        where T : unmanaged, IBinaryInteger<T> => AsPrintableString((ReadOnlySpan<T>)value);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string AsPrintableString<T>(this ReadOnlySpan<T> value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (typeof(T) == typeof(byte))
        {
            ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<T, byte>(value);

            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return Convert.ToHexString(bytes);
            }
        }

        return value.ToString();
    }

    public static ReadOnlySpan<T> AsSpanUnsafe<T>(this ArraySegment<T> segment)
    {
        return MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(segment.Array!), segment.Offset),
            segment.Count
        );
    }

    public static T CreateInstance<T>([DAM(Messages.Ctors)] this Type type, params object?[] parameters)
        where T : class
    {
        try
        {
            return (T)Activator.CreateInstance(type, parameters)!;
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
