using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Serialization;
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

internal static class WriteTest<T, TValue>
        where T : unmanaged, IEquatable<T>
{
    public static async Task WriteRecords(
        CsvWriter<T> writer,
        CsvBindingCollection<TValue> bindingCollection,
        CsvWriterOptions<T> options,
        IEnumerable<TValue> records,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (options.WriteHeader)
            await WriteHeader(writer, bindingCollection, cancellationToken);

        using var enumerator = records.GetEnumerator();

        if (!enumerator.MoveNext())
            return;

        WriteCallback<T, TValue>[] columnCallbacks = CreateWriteCallbacks(bindingCollection, options);

        do
        {
            TValue value = enumerator.Current;

            await columnCallbacks[0](writer, value, cancellationToken);

            for (int i = 1; i < columnCallbacks.Length; i++)
            {
                await writer.WriteDelimiterAsync(cancellationToken);
                await columnCallbacks[i](writer, value, cancellationToken);
            }

            await writer.WriteNewlineAsync(cancellationToken);
        } while (enumerator.MoveNext());
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

    private static WriteCallback<T, TValue>[] CreateWriteCallbacks(
        CsvBindingCollection<TValue> bindingCollection,
        CsvWriterOptions<T> options)
    {
        var bindings = bindingCollection.Bindings;
        var callbacks = new WriteCallback<T, TValue>[bindings.Length];

        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = (MemberCsvBinding<TValue>)bindings[i];
            
            var tvalue = Expression.Parameter(typeof(TValue), "value");
            var (memberExpression, memberType) = binding.Member.GetAsMemberExpression(tvalue);
            var getter = Expression.Lambda(memberExpression, tvalue).CompileLambda<Delegate>();

            callbacks[i] = (WriteCallback<T, TValue>)_getWriteCallbackFn
                .MakeGenericMethod(memberType)
                .Invoke(null, new object[] { getter, options.GetFormatter(memberType) })!;
        }

        return callbacks;
    }

    private static WriteCallback<T, TValue> GetWriteCallback<TProperty>(
        Func<TValue, TProperty> getter,
        ICsvFormatter<T, TProperty> formatter)
    {
        return WriteImpl;

        ValueTask WriteImpl(CsvWriter<T> writer, TValue value, CancellationToken cancellationToken)
        {
            return writer.WriteValueAsync(formatter, getter(value), cancellationToken);
        }
    }
    private static readonly MethodInfo _getWriteCallbackFn = ((Delegate)GetWriteCallback<object>).Method.GetGenericMethodDefinition();
}
