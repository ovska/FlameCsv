using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using FlameCsv.Exceptions;
using FlameCsv.Parsers;
using FlameCsv.Readers;

namespace FlameCsv.Runtime;

/// <summary>
/// State of a CSV row that is being parsed.
/// </summary>
internal abstract partial class CsvRowState
{
    /// <inheritdoc cref="ICsvRowState{T,TResult}.ColumnCount"/>
    public abstract int ColumnCount { get; }

    /// <summary>
    /// Parses the next column from the <paramref name="enumerator"/> to <paramref name="value"/>.
    /// </summary>
    /// <param name="enumerator">Column enumerator</param>
    /// <param name="parser">Parser instance</param>
    /// <param name="value">Target to assign the parsed value to</param>
    /// <typeparam name="T">Token type</typeparam>
    /// <typeparam name="TValue">Parsed value</typeparam>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // should be small enough to inline in Parse()
    protected void ParseNext<T, TValue>(
        ref CsvColumnEnumerator<T> enumerator,
        ICsvParser<T, TValue> parser,
        [MaybeNullWhen(false)] out TValue value)
        where T : unmanaged, IEquatable<T>
    {
        if (enumerator.MoveNext())
        {
            if (parser.TryParse(enumerator.Current, out value))
                return;

            ThrowParseFailed(ref enumerator, parser);
        }

        Unsafe.SkipInit(out value);
        ThrowMoveNextFailed(ref enumerator);
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowMoveNextFailed<T>(ref CsvColumnEnumerator<T> enumerator)
        where T : unmanaged, IEquatable<T>
    {
        throw new CsvFormatException(
            $"Got only {enumerator.Column} columns out of {ColumnCount} in {GetType().ToTypeString()}");
    }

    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    protected void ThrowParseFailed<T>(
        ref CsvColumnEnumerator<T> enumerator,
        ICsvParser<T> parser)
        where T : unmanaged, IEquatable<T>
    {
        throw new CsvParseException(
            $"Failed to parse with {parser.GetType()} from {enumerator.Current.ToString()} "
            + $"in {GetType().ToTypeString()}")
            {
                Parser = parser,
            };
    }

    public static ICsvRowState<T, TResult> Create<T, TResult>((ICsvParser<T> parser, MemberInfo member)[] members)
        where T : unmanaged, IEquatable<T>
    {
        var generics = new List<Type>(members.Length + 2) { typeof(T) };
        generics.AddRange(members.Select(x => ReflectionUtil.MemberType(x.member)));
        generics.Add(typeof(TResult));

        var factoryGenerator = ReflectionUtil.InitializerFactories[members.Length]
            .MakeGenericMethod(generics.Skip(1).ToArray());
        var factory = factoryGenerator.Invoke(null, members.Select(x => (object)x.member).ToArray())!;
        var ctor = GetConstructor<T, TResult>(members.Select(x => ReflectionUtil.MemberType(x.member)));
        List<object> args = new(members.Length + 1) { factory };
        args.AddRange(members.Select(x => x.parser));
        return (ICsvRowState<T, TResult>)ctor.Invoke(args.ToArray());
    }
}
