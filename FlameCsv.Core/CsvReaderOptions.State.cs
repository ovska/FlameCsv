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
    private static readonly ConditionalWeakTable<Type, Func<CsvReaderOptions<T>, object>> _stateFactoryCache = new();

    internal ICsvRowState<T, TResult> BindToState<TResult>()
    {
        if (IndexAttributeBinder.TryGet<TResult>(out var bindings))
            return CreateState(bindings);

        throw new CsvBindingException("TODO");
    }

    /// <summary>
    /// Creates the state object using the bindings and <typeparamref name="TResult"/> type parameter.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal ICsvRowState<T, TResult> CreateState<TResult>(CsvBindingCollection<TResult> bindingCollection)
    {
        if (!_stateFactoryCache.TryGetValue(typeof(TResult), out var stateFactory))
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
            object? valueFactory = factoryGenerator.Invoke(null, factoryGeneratorParameters);

            ConstructorInfo ctor = CsvRowState.GetConstructor<T, TResult>(bindings.Select(b => b.Type));

            stateFactory = GetFactory(bindingCollection, ctor, valueFactory);
            _stateFactoryCache.AddOrUpdate(typeof(TResult), stateFactory);
        }

        return (ICsvRowState<T, TResult>)stateFactory(this);
    }

    private static Func<CsvReaderOptions<T>, object> GetFactory<TResult>(
        CsvBindingCollection<TResult> bindingCollection,
        ConstructorInfo? rowStateConstructor,
        object? valueFactory)
    {
        ArgumentNullException.ThrowIfNull(bindingCollection);
        ArgumentNullException.ThrowIfNull(rowStateConstructor);
        ArgumentNullException.ThrowIfNull(valueFactory);

        Dictionary<int, ICsvParserOverride>? _overrides = new();

        for (int i = 0; i < bindingCollection.Bindings.Length; i++)
        {
            var @override = bindingCollection.Bindings[i].GetParserOverride<TResult>();

            if (@override is not null)
            {
                _overrides[i] = @override;
            }
        }

        if (_overrides.Count == 0)
            _overrides = null;

        var param = Expression.Parameter(typeof(object[]), "args");
        var ctorInvoke = Expression.New(
            rowStateConstructor,
            rowStateConstructor
                .GetParameters()
                .Select(
                    (p, i) => Expression.Convert(
                        Expression.ArrayAccess(param, Expression.Constant(i)),
                        p.ParameterType)));
        var lambda = Expression.Lambda<Func<object[], ICsvRowState<T, TResult>>>(
            Expression.Convert(ctorInvoke, typeof(ICsvRowState<T,TResult>)),
            param);
        var compiled = lambda.CompileFast(flags: CompilerFlags.ThrowOnNotSupportedExpression);

        return options =>
        {
            var bindings = bindingCollection.Bindings;
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
