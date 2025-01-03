using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using FlameCsv.Binding;

namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    public readonly ref struct PrintableState<T>(CsvOptions<T> options, ReadOnlySpan<T> value, int knownNewlineLength)
        where T : unmanaged, IEquatable<T>
    {
        public CsvOptions<T> Options { get; } = options;
        public ReadOnlySpan<T> Value { get; } = value;
        public int KnownNewlineLength { get; } = knownNewlineLength;
    }

    public static string AsPrintableString<T>(this Reading.CsvParser<T> parser, ReadOnlySpan<T> value)
        where T : unmanaged, IEquatable<T>
    {
        return AsPrintableString(parser._options, value, parser._newlineLength);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string AsPrintableString<T>(this CsvOptions<T> options, ReadOnlySpan<T> value, int knownNewlineLength = 0)
        where T : unmanaged, IEquatable<T>
    {
        string? content = options.AllowContentInExceptions ? options.GetAsString(value) : null;

        string structure = string.Create(
            length: value.Length,
            state: new PrintableState<T>(options, value, knownNewlineLength),
            action: static (destination, state) =>
            {
                ref readonly CsvDialect<T> dialect = ref state.Options.Dialect;
                scoped ReadOnlySpan<T> newline = dialect.Newline.Span;

                if (newline.Length == 0)
                {
                    newline = state.Options.GetNewlineSpan(stackalloc T[2]);

                    if (state.KnownNewlineLength == 1)
                    {
                        newline = new ReadOnlySpan<T>(in newline[1]);
                    }
                }

                destination.Fill('x');

                for (int i = 0; i < destination.Length; i++)
                {
                    T token = state.Value[i];

                    if (token.Equals(dialect.Delimiter))
                    {
                        destination[i] = ',';
                    }
                    else if (token.Equals(dialect.Quote))
                    {
                        destination[i] = '"';
                    }
                    else if (dialect.Escape.HasValue && token.Equals(dialect.Escape.Value))
                    {
                        destination[i] = 'E';
                    }
                    else if (newline.Contains(token))
                    {
                        destination[i] = 'N';
                    }
                }
            });

        if (content is null)
            return $"Data structure: [{structure}]";

        return $"Content: [{content}], data structure: [{structure}]";
    }

    public static bool IsValidFor(this CsvBindingScope scope, bool write)
    {
        return scope != (write ? CsvBindingScope.Read : CsvBindingScope.Write);
    }

    public static bool SequenceEquals<T>(in this ReadOnlySequence<T> sequence, ReadOnlySpan<T> other)
        where T : unmanaged, IEquatable<T>
    {
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
        if (typeof(T) == typeof(char) &&
            MemoryMarshal.TryGetString((ReadOnlyMemory<char>)(object)data, out _, out _, out _))
        {
            return data;
        }

        return data.ToArray();
    }

    public static T CreateInstance<T>(
        [DAM(Messages.Ctors)]
        this Type type,
        params object?[] parameters) where T : class
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
