using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

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

    public static void Rethrow(this Exception ex)
    {
        if (ex is not null)
        {
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
    }

    /// <inheritdoc cref="AsPrintableString{T}(ReadOnlySpan{T})"/>
    [ExcludeFromCodeCoverage]
    public static string AsPrintableString<T>(this Span<T> value)
        where T : unmanaged, IBinaryInteger<T> => AsPrintableString((ReadOnlySpan<T>)value);

    /// <summary>
    /// Returns a printable string representation of the value for use in error messages or debugging.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [ExcludeFromCodeCoverage]
    public static string AsPrintableString<T>(this ReadOnlySpan<T> value)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (value.IsEmpty)
        {
            return "<empty>";
        }

        if (typeof(T) == typeof(byte) && !Utf8.IsValid(MemoryMarshal.AsBytes(value)))
        {
            return Convert.ToHexString(MemoryMarshal.AsBytes(value));
        }

        StringBuilder result = new(value.Length);

        foreach (T item in value)
        {
            AppendSingle(result, item);
        }

        return result.ToString();

        static void AppendSingle(StringBuilder sb, T value)
        {
            char c = (char)ushort.CreateTruncating(value);
            switch (c)
            {
                case '\0':
                    sb.Append(@"\0");
                    break;
                case '\r':
                    sb.Append(@"\r");
                    break;
                case '\n':
                    sb.Append(@"\n");
                    break;
                case '\t':
                    sb.Append(@"\t");
                    break;
                case '\f':
                    sb.Append(@"\f");
                    break;
                case '\v':
                    sb.Append(@"\v");
                    break;
                case '\\':
                    sb.Append(@"\\");
                    break;
                case < (char)32 or (char)127:
                    sb.Append($@"\u{(uint)c:X4}");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
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

    public static void EnsureReturned<T>(this ArrayPool<T> pool, ref T[] array)
    {
        T[] local = array;
        array = [];

        if (local.Length > 0)
        {
            pool.Return(local);
        }
    }

    public static OperationCanceledException? GetExceptionIfCanceled(this CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return new OperationCanceledException(token);
        }

        return null;
    }
}
