using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Binding;

namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    public readonly ref struct PrintableState<T>(CsvOptions<T> options, ReadOnlySpan<T> value)
        where T : unmanaged, IBinaryInteger<T>
    {
        public CsvOptions<T> Options { get; } = options;
        public ReadOnlySpan<T> Value { get; } = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string AsPrintableString<T>(this CsvOptions<T> options, ReadOnlySpan<T> value)
        where T : unmanaged, IBinaryInteger<T>
    {
        string structure = string.Create(
            length: value.Length,
            state: new PrintableState<T>(options, value),
            action: static (destination, state) =>
            {
                ref readonly CsvDialect<T> dialect = ref state.Options.Dialect;

                scoped ReadOnlySpan<T> newline = dialect.Newline.IsEmpty
                    ? [T.CreateChecked('\r'), T.CreateChecked('\n')]
                    : dialect.Newline.Span;

                destination.Fill('x');

                for (int i = 0; i < destination.Length; i++)
                {
                    T token = state.Value[i];

                    if (token == dialect.Delimiter)
                    {
                        destination[i] = ',';
                    }
                    else if (token == dialect.Quote)
                    {
                        destination[i] = '"';
                    }
                    else if (dialect.Escape.HasValue && token == dialect.Escape.Value)
                    {
                        destination[i] = 'E';
                    }
                    else if (newline.Contains(token))
                    {
                        destination[i] = 'N';
                    }
                }
            });

        return $"Content: [{options.GetAsString(value)}], data structure: [{structure}]";
    }

    public static bool IsValidFor(this CsvBindingScope scope, bool write)
    {
        return scope != (write ? CsvBindingScope.Read : CsvBindingScope.Write);
    }

    public static bool SequenceEquals<T>(in this ReadOnlySequence<T> sequence, ReadOnlySpan<T> other)
        where T : unmanaged, IBinaryInteger<T>
    {
        if (sequence.Length != other.Length)
            return false;

        if (sequence.IsSingleSegment)
        {
            return sequence.FirstSpan.SequenceEqual(other);
        }

        foreach (var memory in sequence)
        {
            if (!other.StartsWith(memory.Span))
                return false;

            other = other.Slice(memory.Length);
        }

        return true;
    }

    public static ReadOnlyMemory<T> SafeCopy<T>(this ReadOnlyMemory<T> data)
    {
        if (data.IsEmpty)
            return data;

        // strings are immutable and safe to return as-is
        if (typeof(T) == typeof(char)
            && MemoryMarshal.TryGetString((ReadOnlyMemory<char>)(object)data, out _, out _, out _))
        {
            return data;
        }

        return data.ToArray();
    }

    public static T[] UnsafeGetOrCreateArray<T>(this ReadOnlyMemory<T> memory)
    {
        if (MemoryMarshal.TryGetArray(memory, out var segment) &&
            segment is { Array: { } arr, Offset: 0 } &&
            arr.Length == segment.Count)
        {
            return arr;
        }

        return memory.ToArray();
    }

    public static T CreateInstance<T>([DAM(Messages.Ctors)] this Type type, params object?[] parameters) where T : class
    {
        try
        {
            var instance = Activator.CreateInstance(type, parameters)
                ?? throw new InvalidOperationException($"Instance of {type.FullName} could not be created");
            return (T)instance;
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
                    string.Join(", ", parameters.Select(o => o?.GetType().FullName ?? "<null>"))),
                innerException: e);
        }
    }
}
