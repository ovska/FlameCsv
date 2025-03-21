using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using CommunityToolkit.HighPerformance;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.Reading;

/// <summary>
/// Represents a strategy for detecting the delimiter of a CSV file.
/// </summary>
[PublicAPI]
public abstract class CsvDelimiterDetectionStrategy<T> where T : unmanaged, IBinaryInteger<T>
{
    private static PrefixStrategy? _defaultPrefixStrategy;
    private static ProbabilisticStrategy? _defaultProbabilisticStrategy;

    /// <summary>
    /// Provides a hint to the library on how many records the strategy needs to detect the delimiter.
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
    ///     Ranges in the data representing records (not including trailing newline). This value is never empty
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
    /// Returns a strategy that detects the delimiter by using multiple strategies.
    /// </summary>
    /// <param name="first">First strategy to consider</param>
    /// <param name="second">Second strategy to consider</param>
    /// <returns>A compound strategy</returns>
    public static CsvDelimiterDetectionStrategy<T> Either(
        CsvDelimiterDetectionStrategy<T> first,
        CsvDelimiterDetectionStrategy<T> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return new CompoundStrategy(first, second);
    }

    /// <summary>
    /// Returns a strategy that detects the delimiter by checking for the most probable out of specific values.
    /// </summary>
    /// <param name="values">
    /// Values to check for. If empty, <c>[',', ';', '\t', '|']</c> is used.
    /// </param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A single value is provided; at least two values are required.
    /// </exception>
    public static CsvDelimiterDetectionStrategy<T> Values(params ReadOnlySpan<T> values)
    {
        if (values.IsEmpty)
        {
            return _defaultProbabilisticStrategy ??= new ProbabilisticStrategy(
                (T[])
                [
                    T.CreateTruncating(','), T.CreateTruncating(';'), T.CreateTruncating('\t'), T.CreateTruncating('|')
                ]);
        }

        ArgumentOutOfRangeException.ThrowIfEqual(values.Length, 1);
        return new ProbabilisticStrategy(values);
    }

    /// <summary>
    /// Returns a strategy that detects the delimiter by checking if the first record has a specific prefix.
    /// </summary>
    /// <param name="prefix">Prefix to check for</param>
    public static CsvDelimiterDetectionStrategy<T> Prefix(string? prefix = "sep=")
    {
        if (prefix == "sep=")
        {
            return _defaultPrefixStrategy ??= new PrefixStrategy(
                (T[])
                [
                    T.CreateTruncating('s'), T.CreateTruncating('e'), T.CreateTruncating('p'), T.CreateTruncating('=')
                ]);
        }

        if (typeof(T) == typeof(char))
        {
            ReadOnlyMemory<char> asMemory = prefix.AsMemory();
            return _defaultPrefixStrategy
                ??= new PrefixStrategy(Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref asMemory));
        }

        if (typeof(T) == typeof(byte))
        {
            return _defaultPrefixStrategy
                ??= new PrefixStrategy(Unsafe.As<T[]>(Encoding.UTF8.GetBytes(prefix ?? "")));
        }

        throw new NotSupportedException();
    }

    private sealed class CompoundStrategy(
        CsvDelimiterDetectionStrategy<T> first,
        CsvDelimiterDetectionStrategy<T> second)
        : CsvDelimiterDetectionStrategy<T>
    {
        public override int? RecordCountHint
        {
            get
            {
                if (first.RecordCountHint is { } firstHint && second.RecordCountHint is { } secondHint)
                {
                    return Math.Min(firstHint, secondHint);
                }

                return null;
            }
        }

        public override bool TryDetect(
            ReadOnlySpan<T> data,
            ReadOnlySpan<Range> records,
            out T delimiter,
            out int consumedRecords)
        {
            if (second.RecordCountHint < first.RecordCountHint)
            {
                return second.TryDetect(data, records, out delimiter, out consumedRecords) ||
                    first.TryDetect(data, records, out delimiter, out consumedRecords);
            }

            return first.TryDetect(data, records, out delimiter, out consumedRecords) ||
                second.TryDetect(data, records, out delimiter, out consumedRecords);
        }

        public override string ToString() => $"{first} -or- {second}";
    }

    private sealed class PrefixStrategy(ReadOnlyMemory<T> prefix) : CsvDelimiterDetectionStrategy<T>
    {
        public override int? RecordCountHint => 1;

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

    private sealed class ProbabilisticStrategy : CsvDelimiterDetectionStrategy<T>
    {
        private readonly ImmutableArray<T> _values;

        public ProbabilisticStrategy(ReadOnlySpan<T> values)
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
        }

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
