using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Writing;

namespace FlameCsv.Runtime;

internal sealed class ReflectionDematerializer
{
    [RequiresUnreferencedCode(Messages.CompiledExpressions)]
    public static IDematerializer<T, TValue> Create<T, TValue>(in CsvWritingContext<T> context)
        where T : unmanaged, IEquatable<T>
    {
        CsvBindingCollection<TValue> bindingCollection;

        if (context.HasHeader)
        {
            bindingCollection = context.Options.GetHeaderBinder().Bind<TValue>();
        }
        else if (IndexAttributeBinder<TValue>.TryGetBindings(write: true, out var result))
        {
            bindingCollection = result;
        }
        else
        {
            throw new CsvBindingException<TValue>(
                $"Headerless CSV could not be written for {typeof(TValue)}, since the type had no " +
                "[CsvIndex]-attributes and no built-in configuration.");
        }

        var bindings = bindingCollection.MemberBindings;
        var ctor = Dematerializer<T>.GetConstructor(bindings);

        var parameters = new object[bindings.Length + 2];
        parameters[0] = bindingCollection;
        parameters[1] = context.Options;

        var valueParam = Expression.Parameter(typeof(TValue), "value");

        for (int i = 0; i < bindings.Length; i++)
        {
            var (memberExpression, _) = bindings[i].Member.GetAsMemberExpression(valueParam);
            var lambda = Expression.Lambda(memberExpression, valueParam);
            parameters[i + 2] = lambda.CompileLambda<Delegate>();
        }

        var materializer = ctor.Invoke(parameters)!;
        return (IDematerializer<T, TValue>)materializer;
    }
}
