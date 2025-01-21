using System.Runtime.CompilerServices;
using FlameCsv.Exceptions;

namespace FlameCsv.Runtime;

/// <summary>
/// State of a CSV row that is being parsed.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
internal abstract partial class Materializer<T> where T : unmanaged, IBinaryInteger<T>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Parse<TValue>(ReadOnlySpan<T> field, CsvConverter<T, TValue> converter, out TValue? value)
    {
        if (converter.TryParse(field, out value))
        {
            return;
        }

        CsvParseException.Throw(field, converter);
    }

    public override string ToString() => GetType().FullName ?? "";
}
