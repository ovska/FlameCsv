using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FastExpressionCompiler;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Runtime;

namespace FlameCsv;

public sealed partial class CsvReaderOptions<T>
{
    private static readonly ConditionalWeakTable<Type, Func<CsvReaderOptions<T>, object>?> _stateFactoryCache = new();

    internal ICsvRowState<T, TResult> CreateState<TResult>(CsvBindingCollection<TResult> bindingCollection)
    {
        return (ICsvRowState<T, TResult>)CreateStateFactory(bindingCollection)(this);
    }

    internal ICsvRowState<T, TResult> BindToState<TResult>()
    {
        if (!_stateFactoryCache.TryGetValue(typeof(TResult), out var stateFactory))
        {
            if (IndexAttributeBinder.TryGet<TResult>(out var bindings))
            {
                _stateFactoryCache.Add(typeof(TResult), stateFactory = CreateStateFactory(bindings));
            }
            else
            {
                _stateFactoryCache.Add(typeof(TResult), stateFactory = null);
            }
        }

        if (stateFactory is not null)
            return (ICsvRowState<T, TResult>)stateFactory(this);

        throw new CsvBindingException(
            $"CSV has no header and no {nameof(CsvIndexAttribute)} found on members of {typeof(TResult)}");
    }

    /// <summary>
    /// Creates the state object using the bindings and <typeparamref name="TResult"/> type parameter.
    /// </summary>
    [ExcludeFromCodeCoverage]
    private static Func<CsvReaderOptions<T>, object> CreateStateFactory<TResult>(
        CsvBindingCollection<TResult> bindingCollection)
    {
        var bindings = bindingCollection.Bindings;

        // <T0, T1, T2, TResult>
        Type[] genericsWithResult = new Type[bindings.Length + 1];
        for (int i = 0; i < bindings.Length; i++)
        {
            genericsWithResult[i] = bindings[i].Type;
        }

        genericsWithResult[^1] = typeof(TResult);

        var factoryGenerator = ReflectionUtil
            .InitializerFactories[bindings.Length]
            .MakeGenericMethod(genericsWithResult);

        // (member1, member2, member3)
        var factoryGeneratorParameters = new object[bindings.Length];
        for (int i = 0; i < bindings.Length; i++)
        {
            factoryGeneratorParameters[i] = bindings[i].Member;
        }

        // Func<...>, parser1, parser2, parser3
        object? valueFactory = factoryGenerator.Invoke(null, factoryGeneratorParameters)
            ?? throw new InvalidOperationException($"Could not factory for {typeof(TResult)}");

        ConstructorInfo rowStateCtor = CsvRowState.GetConstructor<T, TResult>(bindings.Select(b => b.Type));

        Dictionary<int, ICsvParserOverride>? _overrides = null;

        for (int i = 0; i < bindingCollection.Bindings.Length; i++)
        {
            var @override = bindingCollection.Bindings[i].GetParserOverride<TResult>();

            if (@override is not null)
            {
                (_overrides ??= new())[i] = @override;
            }
        }

        var param = Expression.Parameter(typeof(object[]), "args");
        var ctorInvoke = Expression.New(
            rowStateCtor,
            rowStateCtor
                .GetParameters()
                .Select(
                    (p, i) => Expression.Convert(
                        Expression.ArrayAccess(param, Expression.Constant(i)),
                        p.ParameterType)));
        var lambda = Expression.Lambda<Func<object[], ICsvRowState<T, TResult>>>(
            Expression.Convert(ctorInvoke, typeof(ICsvRowState<T, TResult>)),
            param);
        var compiled = lambda.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression);

        return options =>
        {
            object[] rowStateConstructorArgs = new object[bindings.Length + 1];
            rowStateConstructorArgs[0] = valueFactory;

            for (int i = 0; i < bindings.Length; i++)
            {
                if (_overrides is not null && _overrides.TryGetValue(i, out var @override))
                {
                    rowStateConstructorArgs[i + 1] = @override.CreateParser(bindings[i], options);
                }
                else
                {
                    rowStateConstructorArgs[i + 1] = options.GetParser(bindings[i].Type);
                }
            }

            return compiled(rowStateConstructorArgs);
        };
    }
}
