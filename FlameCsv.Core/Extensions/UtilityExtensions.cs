using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Binding;

namespace FlameCsv.Extensions;

internal static class UtilityExtensions
{
    public static string AsPrintableString<T>(this Reading.CsvParser<T> parser, ReadOnlyMemory<T> value)
        where T : unmanaged, IEquatable<T>
    {
        return AsPrintableString(parser._options, value);
    }

[MethodImpl(MethodImplOptions.NoInlining)]
    public static string AsPrintableString<T>(this CsvOptions<T> options, ReadOnlyMemory<T> value)
        where T : unmanaged, IEquatable<T>
    {
        string? content = options._allowContentInExceptions ? options.GetAsString(value.Span) : null;

        string structure = string.Create(
            length: value.Length,
            state: (options, value),
            action: static (destination, state) =>
            {
                (CsvOptions<T> options, ReadOnlyMemory<T> memory) = state;
                var source = memory.Span;

                var newline = options._newline.Span;

                destination.Fill('x');

                for (int i = 0; i < destination.Length; i++)
                {
                    T token = source[i];

                    if (token.Equals(options._delimiter))
                    {
                        destination[i] = ',';
                    }
                    else if (token.Equals(options._quote))
                    {
                        destination[i] = '"';
                    }
                    else if (options._escape.HasValue && token.Equals(options._escape.Value))
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
        [DynamicallyAccessedMembers(Messages.Ctors)]
        this Type type,
        params object?[] parameters) where T : class
    {
        try
        {
            var instance = Activator.CreateInstance(type, parameters)
                ?? throw new InvalidOperationException($"Instance of {type.ToTypeString()} could not be created");
            return (T)instance;
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Could not create {0} from type {1} and {2} constructor parameters: [{3}]",
                    typeof(T).ToTypeString(),
                    type.ToTypeString(),
                    parameters.Length,
                    string.Join(", ", parameters.Select(o => o?.GetType().ToTypeString() ?? "<null>"))),
                innerException: e);
        }
    }
}
