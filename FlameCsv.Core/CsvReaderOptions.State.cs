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

internal static class CsvReaderOptionsExtension
{
    private delegate object StateFactory<T>(CsvReaderOptions<T> options) where T : unmanaged, IEquatable<T>;

    private static IReadOnlySet<Type> ValueTuples { get; } = new HashSet<Type>
    {
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>)
    };

    private static class FactoryCache<T> where T : unmanaged, IEquatable<T>
    {
        public static readonly ConditionalWeakTable<Type, StateFactory<T>?> Value = new();
    }

    public static ICsvRowState<T, TResult> CreateState<T, TResult>(
        this CsvReaderOptions<T> options,
        CsvBindingCollection<TResult> bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        return (ICsvRowState<T, TResult>)CreateStateFactory<T, TResult>(bindingCollection)(options);
    }

    public static ICsvRowState<T, TResult> BindToState<T, TResult>(this CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        if (!FactoryCache<T>.Value.TryGetValue(typeof(TResult), out var stateFactory))
        {
            if (TryGetBuiltinFactory<T, TResult>(out stateFactory))
            {
            }
            else if (IndexAttributeBinder.TryGet<TResult>(out var bindings))
            {
                stateFactory = CreateStateFactory<T, TResult>(bindings);
            }
            else
            {
                stateFactory = null;
            }

            FactoryCache<T>.Value.Add(typeof(TResult), stateFactory);
        }

        if (stateFactory is not null)
            return (ICsvRowState<T, TResult>)stateFactory(options);

        throw new CsvBindingException(
            $"CSV has no header and no {nameof(CsvIndexAttribute)} found on members of {typeof(TResult)}");
    }

    private static bool TryGetBuiltinFactory<T, TResult>([NotNullWhen(true)] out StateFactory<T>? factory)
        where T : unmanaged, IEquatable<T>
    {
        if (typeof(TResult).IsValueType && ValueTuples.Contains(typeof(TResult)))
        {
            var ctor = typeof(TResult).GetConstructors()[0];
            var ctorParams = ctor.GetParameters();
            var factoryParams = ctorParams.Select(static p => Expression.Parameter(p.ParameterType)).ToArray();
            var newExpr = Expression.New(ctor, factoryParams);
            var lambda = Expression.Lambda(newExpr, factoryParams);


            //factory = CreateStateFactory<T,TResult>();
        }

        factory = default;
        return false;
    }

    private static StateFactory<T> CreateStateFactory<T, TResult>(
        CsvBindingCollection<TResult> bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        return CreateStateFactory<T, TResult>(bindingCollection, CreateValueFactory(bindingCollection));
    }

/// <summary>
/// Creates the state object using the bindings and <typeparamref name="TResult"/> type parameter.
/// </summary>
[ExcludeFromCodeCoverage]
    private static StateFactory<T> CreateStateFactory<T, TResult>(
        CsvBindingCollection<TResult> bindingCollection,
        object valueFactory)
        where T : unmanaged, IEquatable<T>
    {
        var bindings = bindingCollection.Bindings;

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

    private static object CreateValueFactory<TResult>(CsvBindingCollection<TResult> bindingCollection)
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
        return factoryGenerator.Invoke(null, factoryGeneratorParameters)
            ?? throw new InvalidOperationException($"Could not factory for {typeof(TResult)}");
    }
}
