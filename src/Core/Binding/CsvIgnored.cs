using System.Runtime.CompilerServices;

namespace FlameCsv.Binding;

/// <summary>
/// Sentinel type for ignored field when reading/writing CSV.
/// </summary>
/// <remarks>
/// For example, when reading/writing tuples or value-tuples, you can use this type
/// in one position to ignore the field in that index when reading, or always write an empty
/// value when writing.
/// </remarks>
public readonly struct CsvIgnored
{
    /// <summary>
    /// Returns a no-op converter instance of <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// This converter will always return <c>true</c> without writing or reading anything.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CsvConverter<T, TResult> Converter<T, TResult>()
        where T : unmanaged, IBinaryInteger<T> => IgnoredConverter<T, TResult>.Instance;

    private sealed class IgnoredConverter<T, TResult> : CsvConverter<T, TResult>
        where T : unmanaged, IBinaryInteger<T>
    {
        public static readonly IgnoredConverter<T, TResult> Instance = new();

        /// <summary>
        /// Always returns <c>true</c> without writing anything.
        /// </summary>
        public override bool TryFormat(Span<T> destination, TResult value, out int charsWritten)
        {
            charsWritten = 0;
            return true;
        }

        /// <summary>
        /// Always returns <c>true</c>.
        /// </summary>
        public override bool TryParse(ReadOnlySpan<T> source, out TResult value)
        {
            value = default!;
            return true;
        }
    }
}
