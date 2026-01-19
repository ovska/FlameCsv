using System.Buffers;
using System.Text;
using System.Text.Unicode;
using JetBrains.Annotations;

namespace FlameCsv.Utilities;

internal static class Utf8Util
{
    public static TResult WithBytes<TState, TResult>(
        ReadOnlySpan<byte> input,
        TState state,
        [RequireStaticDelegate] Func<ReadOnlySpan<char>, TState, TResult> func
    )
        where TState : allows ref struct
    {
        scoped Span<char> buffer;
        char[]? toReturn = null;
        int length = Encoding.UTF8.GetMaxCharCount(input.Length);

        if (length <= 128)
        {
            buffer = stackalloc char[128];
        }
        else
        {
            buffer = toReturn = ArrayPool<char>.Shared.Rent(length);
        }

        int written = Encoding.UTF8.GetChars(input, buffer);

        TResult result = func(buffer[..written], state);

        if (toReturn is not null)
            ArrayPool<char>.Shared.Return(toReturn);

        return result;
    }

    public static TResult WithChars<TState, TResult>(
        ReadOnlySpan<char> input,
        TState state,
        [RequireStaticDelegate] Func<ReadOnlySpan<byte>, TState, TResult> func
    )
        where TState : allows ref struct
    {
        scoped Span<byte> buffer;
        byte[]? toReturn = null;
        int length = Encoding.UTF8.GetMaxByteCount(input.Length);

        if (length <= 256)
        {
            buffer = stackalloc byte[256];
        }
        else
        {
            buffer = toReturn = ArrayPool<byte>.Shared.Rent(length);
        }

        int written = Encoding.UTF8.GetBytes(input, buffer);

        TResult result = func(buffer[..written], state);

        if (toReturn is not null)
            ArrayPool<byte>.Shared.Return(toReturn);

        return result;
    }
}
