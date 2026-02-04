using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using FlameCsv.Utilities;

namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCRLF(this CsvNewline newline)
    {
        // hoisting the check to a local allows JIT to "see" the constant and eliminate one jump instruction
        bool isWindows = Environment.NewLine == "\r\n";
        return newline is CsvNewline.CRLF || (isWindows && newline is CsvNewline.Platform);
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
            return $"Hex: [{Convert.ToHexString(MemoryMarshal.AsBytes(value))}]";
        }

        ValueStringBuilder vsb = RuntimeHelpers.TryEnsureSufficientExecutionStack()
            ? new(stackalloc char[256])
            : new(value.Length);

        foreach (T item in value)
        {
            char c = (char)ushort.CreateTruncating(item);

            switch (c)
            {
                case '\\':
                    vsb.Append(@"\\");
                    break;
                case >= ' ' and <= '~':
                    vsb.Append(c);
                    break;
                case '\0':
                    vsb.Append(@"\0");
                    break;
                case '\r':
                    vsb.Append(@"\r");
                    break;
                case '\n':
                    vsb.Append(@"\n");
                    break;
                case '\t':
                    vsb.Append(@"\t");
                    break;
                case '\f':
                    vsb.Append(@"\f");
                    break;
                case '\v':
                    vsb.Append(@"\v");
                    break;
                case '\e':
                    vsb.Append(@"\e");
                    break;
                default:
                    vsb.Append($@"\u{(uint)c:X4}");
                    break;
            }
        }

        return vsb.ToString();
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
