using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Class for detecting the delimiter used in a CSV file.
/// </summary>
[PublicAPI]
public abstract class CsvDelimiterDetector<T> where T : unmanaged, IBinaryInteger<T>
{
    private static PrefixDetector? _defaultPrefixDetector;
    private static ProbabilisticDetector? _defaultProbabilisticOptional;
    private static ProbabilisticDetector? _defaultProbabilisticRequired;

    /// <summary>
    /// Whether the configured delimiter should be used if the detector fails to detect one.
    /// </summary>
    public abstract bool IsOptional { get; }

    /// <summary>
    /// Provides a hint to the library on how many records the detector needs to detect the delimiter.
    /// </summary>
    /// <remarks>
    /// This value may be ignored by the implementation if it is too large, or otherwise impractical to use.
    /// </remarks>
    public virtual int? RecordCountHint => null;

    /// <summary>
    /// Attemps to detect the delimiter used in the data.
    /// </summary>
    /// <param name="data">Data available to determine the delimiter</param>
    /// <param name="records">
    /// Ranges in the data representing records (not including trailing newline). This value is never empty
    /// </param>
    /// <param name="delimiter">Detected delimiter</param>
    /// <param name="consumedRecords">How many records should be skipped if the detection succeeds</param>
    /// <returns><see langword="true"/> if the delimiter was detected, otherwise <see langword="false"/></returns>
    public abstract bool TryDetect(
        ReadOnlySpan<T> data,
        ReadOnlySpan<Range> records,
        out T delimiter,
        out int consumedRecords);

    /// <summary>
    /// Returns a detector that evaluates the most probable out of specific values,
    /// throwing an exception if no best-match is found among the candidates.
    /// </summary>
    /// <param name="values">
    /// Values to check for. If empty, <c>[',', ';', '\t', '|']</c> is used.
    /// </param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A single value is provided; at least two values are required.
    /// </exception>
    public static CsvDelimiterDetector<T> Values(params ReadOnlySpan<T> values)
    {
        return ValuesCore(values, optional: false);
    }

    /// <summary>
    /// Returns a detector that evaluates the most probable out of specific values,
    /// falling back to the configured delimiter if no best-match is found among the candidates.
    /// </summary>
    /// <param name="values">
    /// Values to check for. If empty, <c>[',', ';', '\t', '|']</c> is used.
    /// </param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A single value is provided; at least two values are required.
    /// </exception>
    public static CsvDelimiterDetector<T> ValuesOptional(params ReadOnlySpan<T> values)
    {
        return ValuesCore(values, optional: false);
    }

    private static ProbabilisticDetector ValuesCore(ReadOnlySpan<T> values, bool optional)
    {
        if (values.IsEmpty)
        {
            ref ProbabilisticDetector? dst = ref optional
                ? ref _defaultProbabilisticOptional
                : ref _defaultProbabilisticRequired;

            return dst ??= new ProbabilisticDetector(
                (T[])
                [
                    T.CreateTruncating(','), T.CreateTruncating(';'), T.CreateTruncating('\t'), T.CreateTruncating('|')
                ],
                optional);
        }

        ArgumentOutOfRangeException.ThrowIfEqual(values.Length, 1);
        return new ProbabilisticDetector(values, optional);
    }

    /// <summary>
    /// Returns a detector that checks if the first record has a specific prefix.
    /// </summary>
    /// <param name="prefix">Prefix to check for</param>
    /// <param name="isOptional">Whether the configured delimiter should be used if </param>
    public static CsvDelimiterDetector<T> Prefix(string? prefix = "sep=", bool isOptional = true)
    {
        if (prefix == "sep=")
        {
            return _defaultPrefixDetector ??= new PrefixDetector(
                (T[])
                [
                    T.CreateTruncating('s'), T.CreateTruncating('e'), T.CreateTruncating('p'), T.CreateTruncating('=')
                ],
                isOptional);
        }

        if (typeof(T) == typeof(char))
        {
            ReadOnlyMemory<char> asMemory = prefix.AsMemory();
            return _defaultPrefixDetector
                ??= new PrefixDetector(Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref asMemory),
                    isOptional);
        }

        if (typeof(T) == typeof(byte))
        {
            return _defaultPrefixDetector
                ??= new PrefixDetector(Unsafe.As<T[]>(Encoding.UTF8.GetBytes(prefix ?? "")), isOptional);
        }

        throw new NotSupportedException();
    }

    private sealed class PrefixDetector(ReadOnlyMemory<T> prefix, bool isOptional) : CsvDelimiterDetector<T>
    {
        public override int? RecordCountHint => 1;

        public override bool IsOptional => isOptional;

        /// <inheritdoc/>
        public override bool TryDetect(
            ReadOnlySpan<T> data,
            ReadOnlySpan<Range> records,
            out T delimiter,
            out int consumedRecords)
        {
            ReadOnlySpan<T> first = data[records[0]];

            if (first.Length == prefix.Length + 1 &&
                first.StartsWith(prefix.Span))
            {
                delimiter = first[^1];
                consumedRecords = 1;
                return true;
            }

            delimiter = default;
            consumedRecords = 0;
            return false;
        }

        public override string ToString()
        {
            if (typeof(T) == typeof(char))
            {
                return "Prefix: " + prefix.Span.Cast<T, char>().ToString();
            }

            if (typeof(T) == typeof(byte))
            {
                return "Prefix: " + Encoding.UTF8.GetString(prefix.Span.Cast<T, byte>());
            }

            return $"Prefix {prefix}";
        }
    }

    private sealed class ProbabilisticDetector : CsvDelimiterDetector<T>
    {
        private readonly ImmutableArray<T> _values;

        public ProbabilisticDetector(ReadOnlySpan<T> values, bool isOptional)
        {
            ArgumentOutOfRangeException.ThrowIfZero(values.Length);

            using ValueListBuilder<T> list = new(stackalloc T[8]);

            foreach (T value in values)
            {
                if (!list.AsSpan().Contains(value))
                {
                    list.Append(value);
                }
            }

            _values = [..list.AsSpan()];
            IsOptional = isOptional;
        }

        public override bool IsOptional { get; }

        /// <inheritdoc/>
        public override bool TryDetect(
            ReadOnlySpan<T> data,
            ReadOnlySpan<Range> records,
            out T delimiter,
            out int consumedRecords)
        {
            consumedRecords = 0;

            using ValueListBuilder<int> counts = new(stackalloc int[16]);
            using ValueListBuilder<int> fieldCounts = new(stackalloc int[16]);

            int bestDelimiterIndex = -1;
            double bestScore = double.MinValue;

            for (int index = 0; index < _values.Length; index++)
            {
                T value = _values[index];
                counts.Clear();
                fieldCounts.Clear();
                int total = 0;

                foreach (var range in records)
                {
                    ReadOnlySpan<T> record = data[range];
                    int count = 0;

                    // Count occurrences (ignoring quoted content would be better but requires more complex parsing)
                    for (int i = 0; i < record.Length; i++)
                    {
                        if (record[i] == value) count++;
                    }

                    counts.Append(count);
                    total += count;

                    // Store field count (delimiters + 1) for consistency check
                    fieldCounts.Append(count + 1);
                }

                if (total == 0) continue; // Skip delimiters that don't appear at all

                double average = total / (double)records.Length;
                double variance = 0;

                // Calculate variance of delimiter occurrences
                foreach (var count in counts.AsSpan())
                {
                    variance += (count - average) * (count - average);
                }

                variance /= records.Length;

                // Calculate field count consistency (variance of field counts)
                double fieldAverage = Sum(fieldCounts.AsSpan()) / (double)fieldCounts.Length;
                double fieldVariance = 0;

                foreach (var count in fieldCounts.AsSpan())
                {
                    fieldVariance += (count - fieldAverage) * (count - fieldAverage);
                }

                fieldVariance /= fieldCounts.Length;

                // Scoring formula: balance between high frequency, low variance, and consistent fields
                // We want high average, low variance, and low field variance
                double score = (average * 2) - variance - (fieldVariance * 3);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDelimiterIndex = index;
                }
            }

            if (bestDelimiterIndex >= 0)
            {
                delimiter = _values[bestDelimiterIndex];
                return true;
            }

            delimiter = default;
            return false;

            static int Sum(ReadOnlySpan<int> span)
            {
                int sum = 0;
                foreach (int value in span) sum += value;
                return sum;
            }
        }

        public override string ToString()
        {
            if (typeof(T) == typeof(char))
            {
                return "Values: " + _values.AsSpan().Cast<T, char>().ToString();
            }

            if (typeof(T) == typeof(byte))
            {
                return "Values: " + Encoding.UTF8.GetString(_values.AsSpan().Cast<T, byte>());
            }

            return $"Values: {_values}";
        }
    }
}
