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
internal delegate ValueTask WriteCallback<T, TWriter, TValue>(
       CsvWriteOperation<T, TWriter> writer,
       TValue value,
       CancellationToken cancellationToken)
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IAsyncBufferWriter<T>;

internal static class WriteTest<T, TWriter, TValue>
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IAsyncBufferWriter<T>
{
    public static async Task WriteRecords(
        CsvWriteOperation<T, TWriter> writer,
        CsvBindingCollection<TValue> bindingCollection,
        CsvWriterOptions<T> options,
        IEnumerable<TValue> records,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (options.WriteHeader)
                await WriteHeader(writer, bindingCollection, cancellationToken);

            using var enumerator = records.GetEnumerator();

            if (!enumerator.MoveNext())
                return;

            WriteCallback<T, TWriter, TValue>[] columnCallbacks = CreateWriteCallbacks(bindingCollection, options);

            do
            {
                TValue value = enumerator.Current;

                await columnCallbacks[0](writer, value, cancellationToken);

                for (int i = 1; i < columnCallbacks.Length; i++)
                {
                    writer.WriteDelimiter();
                    await columnCallbacks[i](writer, value, cancellationToken);
                }

                writer.WriteNewline();
            } while (enumerator.MoveNext());
        }
        catch (Exception e)
        {
            writer.Exception = e;
        }
        finally
        {
            await writer.DisposeAsync();
        }
    }

    private static async ValueTask WriteHeader(
        CsvWriteOperation<T, TWriter> writer,
        CsvBindingCollection<TValue> bindingCollection,
        CancellationToken cancellationToken)
    {
        var formatter = DefaultFormatters.Binding<T, TValue>();

        await writer.WriteValueAsync(formatter, bindingCollection.Bindings[0], cancellationToken);

        for (int i = 1; i < bindingCollection.MemberBindings.Length; i++)
        {
            writer.WriteDelimiter();
            await writer.WriteValueAsync(formatter, bindingCollection.Bindings[i], cancellationToken);
        }

        writer.WriteNewline();
    }

    private static WriteCallback<T, TWriter, TValue>[] CreateWriteCallbacks(
        CsvBindingCollection<TValue> bindingCollection,
        CsvWriterOptions<T> options)
    {
        var bindings = bindingCollection.Bindings;
        var callbacks = new WriteCallback<T, TWriter, TValue>[bindings.Length];

        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = (MemberCsvBinding<TValue>)bindings[i];

            var tvalue = Expression.Parameter(typeof(TValue), "value");
            var (memberExpression, memberType) = binding.Member.GetAsMemberExpression(tvalue);
            var getter = Expression.Lambda(memberExpression, tvalue).CompileLambda<Delegate>();

            callbacks[i] = (WriteCallback<T, TWriter, TValue>)_getWriteCallbackFn
                .MakeGenericMethod(memberType)
                .Invoke(null, new object[] { getter, options.GetFormatter(memberType) })!;
        }

        return callbacks;
    }

    private static WriteCallback<T, TWriter, TValue> GetWriteCallback<TProperty>(
        Func<TValue, TProperty> getter,
        ICsvFormatter<T, TProperty> formatter)
    {
        return WriteImpl;

        ValueTask WriteImpl(CsvWriteOperation<T, TWriter> writer, TValue value, CancellationToken cancellationToken)
        {
            return writer.WriteValueAsync(formatter, getter(value), cancellationToken);
        }
    }
    private static readonly MethodInfo _getWriteCallbackFn = ((Delegate)GetWriteCallback<object>).Method.GetGenericMethodDefinition();
}
