using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using FlameCsv.Binding;
using FlameCsv.Binding.Internal;
using FlameCsv.Extensions;
using FlameCsv.Writing;

namespace FlameCsv.Runtime;

/// <summary>
/// Callback to write a field or a property to the writer.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Record type</typeparam>
/// <param name="writer">Writer instance</param>
/// <param name="value">Record instance</param>
internal delegate void WriteCallback<T, TWriter, TValue>(CsvRecordWriter<T, TWriter> writer, TValue value)
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IAsyncBufferWriter<T>;

internal static class WriteTest<T, TWriter, TValue>
    where T : unmanaged, IEquatable<T>
    where TWriter : struct, IAsyncBufferWriter<T>
{
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static async Task WriteRecords(
        CsvRecordWriter<T, TWriter> writer,
        CsvBindingCollection<TValue> bindingCollection,
        CsvOptions<T> options,
        IEnumerable<TValue> records,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (options.HasHeader)
                WriteHeader(writer, bindingCollection);

            using var enumerator = records.GetEnumerator();

            if (!enumerator.MoveNext())
                return;

            WriteCallback<T, TWriter, TValue> writeValue = CreateWriteCallback(bindingCollection, options);

            do
            {
                if (writer.NeedsFlush)
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                writeValue(writer, enumerator.Current);
            }
            while (enumerator.MoveNext());
        }
        catch (Exception e)
        {
            writer.Exception = e;
        }
        finally
        {
            await writer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static void WriteHeader(CsvRecordWriter<T, TWriter> writer, CsvBindingCollection<TValue> bindingCollection)
    {
        writer.WriteString(bindingCollection.MemberBindings[0].Member.Name);

        for (int i = 1; i < bindingCollection.MemberBindings.Length; i++)
        {
            writer.WriteDelimiter();
            writer.WriteString(bindingCollection.MemberBindings[i].Member.Name);
        }

        writer.WriteNewline();
    }

    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    private static WriteCallback<T, TWriter, TValue> CreateWriteCallback(
        CsvBindingCollection<TValue> bindingCollection,
        CsvOptions<T> options)
    {
        /*
        * (writer, value) =>
        * {
        *     writer.WriteValue(formatter1, value.Member1);
        *     writer.WriteDelimiter();
        *     writer.WriteValue(formatter2, value.Member2);
        *     writer.WriteDelimiter();
        *     writer.WriteValue(formatter3, value.Member3);
        *     writer.WriteNewline();
        * }
        */

        var bindings = bindingCollection.Bindings;

        var writerParam = Expression.Parameter(typeof(CsvRecordWriter<T, TWriter>), "writer");
        var valueParam = Expression.Parameter(typeof(TValue), "value");

        var methodBody = new List<Expression>(bindings.Length * 2);

        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = (MemberCsvBinding<TValue>)bindings[i];

            var (memberExpression, memberType) = binding.Member.GetAsMemberExpression(valueParam);
            var formatter = Expression.Constant(options.GetConverter(memberType), typeof(CsvConverter<,>).MakeGenericType(typeof(T), memberType));

            var writeValueCall = Expression.Call(
                writerParam,
                _writeValueMethod.MakeGenericMethod(memberType),
                formatter,
                memberExpression);

            var writeDelimiterOrNewlineCall = (i == bindings.Length - 1)
                ? Expression.Call(writerParam, _writeNewlineMethod)
                : Expression.Call(writerParam, _writeDelimiterMethod);

            methodBody.Add(writeValueCall);
            methodBody.Add(writeDelimiterOrNewlineCall);
        }

        var lambda = Expression.Lambda<WriteCallback<T, TWriter, TValue>>(Expression.Block(methodBody), writerParam, valueParam);
        return lambda.CompileLambdaWithClosure<WriteCallback<T, TWriter, TValue>>();
    }

    private static readonly MethodInfo _writeValueMethod = (typeof(CsvRecordWriter<T, TWriter>).GetMethod("WriteValue"))!;
    private static readonly MethodInfo _writeDelimiterMethod = (typeof(CsvRecordWriter<T, TWriter>).GetMethod("WriteDelimiter"))!;
    private static readonly MethodInfo _writeNewlineMethod = (typeof(CsvRecordWriter<T, TWriter>).GetMethod("WriteNewline"))!;
}
