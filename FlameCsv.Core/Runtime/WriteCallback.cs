using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using FlameCsv.Binding;
using FlameCsv.Binding.Internal;
using FlameCsv.Extensions;
using FlameCsv.Formatters;
using FlameCsv.Formatters.Internal;
using FlameCsv.Writers;

namespace FlameCsv.Runtime;

/// <summary>
/// Callback to write a field or a property to the writer.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Record type</typeparam>
/// <param name="writer">Writer instance</param>
/// <param name="value">Record instance</param>
/// <param name="cancellationToken">Token to cancel the asynchronous write operation</param>
internal delegate ValueTask WriteCallback<T, TValue>(
       CsvWriter<T> writer,
       TValue value,
       CancellationToken cancellationToken)
    where T : unmanaged, IEquatable<T>;

internal readonly struct CsvRecordWriter<T, TValue> where T : unmanaged, IEquatable<T>
{
    private readonly WriteCallback<T, TValue>[] _columnWriters;

    public CsvRecordWriter(WriteCallback<T, TValue>[] columnWriters)
    {
        _columnWriters = columnWriters;
    }

    public async ValueTask WriteRecordAsync(
        CsvWriter<T> writer,
        TValue value,
        CancellationToken cancellationToken)
    {
        await _columnWriters[0](writer, value, cancellationToken);

        for (int i = 1; i < _columnWriters.Length; i++)
        {
            await writer.WriteDelimiterAsync(cancellationToken);
            await _columnWriters[i](writer, value, cancellationToken);
        }
    }
}

internal static class WriteTest<T, TValue>
        where T : unmanaged, IEquatable<T>
{
    public static async ValueTask WriteRecords(
        CsvWriter<T> writer,
        CsvBindingCollection<TValue> bindingCollection,
        CsvWriterOptions<T> options,
        IEnumerable<TValue> records,
        CancellationToken cancellationToken)
    {
        if (options.WriteHeader)
            await WriteHeader(writer, bindingCollection, cancellationToken);

        // TODO: write header?
        WriteCallback<T, TValue>[] columnCallbacks = CsvWriterReflection<T, TValue>.CreateWriteCallbacks(bindingCollection, options);

        foreach (var value in records)
        {
            await columnCallbacks[0](writer, value, cancellationToken);

            for (int i = 1; i < columnCallbacks.Length; i++)
            {
                await writer.WriteDelimiterAsync(cancellationToken);
                await columnCallbacks[i](writer, value, cancellationToken);
            }


            await writer.WriteNewlineAsync(cancellationToken);
        }
    }

    private static async ValueTask WriteHeader(
        CsvWriter<T> writer,
        CsvBindingCollection<TValue> bindingCollection,
        CancellationToken cancellationToken)
    {
        var formatter = DefaultFormatters.Binding<T, TValue>();

        for (int i = 0; i < bindingCollection.MemberBindings.Length; i++)
        {
            if (i > 0)
            {
                await writer.WriteDelimiterAsync(cancellationToken);
            }

            await writer.WriteValueAsync(formatter, bindingCollection.Bindings[i], cancellationToken);
        }

        await writer.WriteNewlineAsync(cancellationToken);
    }



    private delegate bool TryFormatMember(MemberInfo memberInfo, Span<T> destination, out int tokensWritten);
}

internal static class CsvWriterReflection<T, TValue> where T : unmanaged, IEquatable<T>
{
    private static readonly MethodInfo _createFuncMethod = ((Delegate)CreateFunc<object>).Method.GetGenericMethodDefinition();

    public static WriteCallback<T, TValue> CreateFunc(
        MemberInfo member,
        ICsvFormatter<T> formatter)
    {
        var tvalue = Expression.Parameter(typeof(TValue), "value");
        var (memberExpression, memberType) = member.GetAsMemberExpression(tvalue);
        var propertyFunc = Expression.Lambda(memberExpression, tvalue).CompileLambda<Delegate>();

        return (WriteCallback<T, TValue>?)_createFuncMethod
            .MakeGenericMethod(memberType)
            .Invoke(null, new object[] { propertyFunc, formatter })
            ?? throw new InvalidOperationException("Could not get write delegate for " + member);
    }

    private static WriteCallback<T, TValue> CreateFunc<TProperty>(
        Delegate propertyFunc,
        ICsvFormatter<T> formatter)
    {
        var propFn = (Func<TValue, TProperty>)propertyFunc;
        var formatterTyped = (ICsvFormatter<T, TProperty>)formatter;

        return WriteImpl;

        ValueTask WriteImpl(
            CsvWriter<T> writer,
            TValue value,
            CancellationToken cancellationToken)
        {
            return writer.WriteValueAsync(formatterTyped, propFn(value), cancellationToken);
        }
    }

    public static WriteCallback<T, TValue>[] CreateWriteCallbacks(
        CsvBindingCollection<TValue> bindingCollection,
        CsvWriterOptions<T> options)
    {
        var bindings = bindingCollection.Bindings;
        var callbacks = new WriteCallback<T, TValue>[bindings.Length];

        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = (MemberCsvBinding<TValue>)bindings[i];
            callbacks[i] = CreateFunc(binding.Member, options.GetFormatter(binding.Type));
        }

        return callbacks;
    }
}
