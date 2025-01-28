using System.Runtime.CompilerServices;

namespace FlameCsv.Binding;

/// <summary>
/// Sentinel type for ignored column when reading/writing CSV.
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
    /// This converter will always return <see langword="true"/> without writing or reading anything.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static CsvConverter<T, CsvIgnored> Converter<T>()
        where T : unmanaged, IBinaryInteger<T>
        => IgnoredConverter<T>.Instance;

    private sealed class IgnoredConverter<T> : CsvConverter<T, CsvIgnored> where T : unmanaged, IBinaryInteger<T>
    {
        public static readonly IgnoredConverter<T> Instance = new();

        /// <summary>
        /// Always returns <see langword="true"/> without writing anything.
        /// </summary>
        public override bool TryFormat(Span<T> destination, CsvIgnored value, out int charsWritten)
        {
            charsWritten = 0;
            return true;
        }

        /// <summary>
        /// Always returns <see langword="true"/>.
        /// </summary>
        public override bool TryParse(ReadOnlySpan<T> source, out CsvIgnored value)
        {
            value = default;
            return true;
        }
    }
}
