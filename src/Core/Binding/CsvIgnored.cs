using System.Runtime.CompilerServices;

namespace FlameCsv.Binding;

/// <summary>
/// Sentinel type for ignored field when reading/writing CSV.
/// This is used internally to ignore specific fields during reading or writing.
/// </summary>
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

        protected internal override bool CanFormatNull => true;

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
