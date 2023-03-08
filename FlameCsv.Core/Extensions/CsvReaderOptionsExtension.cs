using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Parsers;
using FlameCsv.Runtime;

namespace FlameCsv.Extensions;

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
        ConstructorInfo rowStateCtor = CsvRowState.GetConstructor<T, TResult>(bindingCollection.Bindings);

        // don't create the dictionary unless needed, overrides should be relatively rare
        Dictionary<int, CsvParserOverrideAttribute>? _overrides = null;

        for (int i = 0; i < bindingCollection.Bindings.Length; i++)
        {
            if (bindingCollection.Bindings[i].TryGetParserOverride(out var @override))
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
        var stateFactory = lambda.CompileLambda<Func<object[], ICsvRowState<T, TResult>>>();

        return CreateStateImpl;

        ICsvRowState<T, TResult> CreateStateImpl(CsvReaderOptions<T> options)
        {
            var bindings = bindingCollection.Bindings;

            object[] rowStateConstructorArgs = new object[bindings.Length + 1];
            rowStateConstructorArgs[0] = valueFactory;

            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].IsIgnored)
                {
                    rowStateConstructorArgs[i + 1] = NoOpParser<T>.Instance;
                }
                else if (_overrides is not null && _overrides.TryGetValue(i, out var @override))
                {
                    rowStateConstructorArgs[i + 1] = @override.CreateParser(bindings[i], options);
                }
                else
                {
                    rowStateConstructorArgs[i + 1] = options.GetParser(bindings[i].Type);
                }
            }

            return stateFactory(rowStateConstructorArgs);
        }
    }

    internal static Delegate CreateValueFactory<TResult>(CsvBindingCollection<TResult> bc)
    {
        ParameterExpression[] parameters = GetParametersByBindingIndex();
        NewExpression newExpr = GetObjectInitialization();
        Expression body = GetExpressionBody();
        return Expression.Lambda(body, parameters).CompileLambda<Delegate>();

        ParameterExpression[] GetParametersByBindingIndex()
        {
            var array = new ParameterExpression[bc.Bindings.Length];

            foreach (var binding in bc.Bindings)
            {
                // TODO: figure out how to completely skip ignored columns
                array[binding.Index] = Expression.Parameter(binding.Type
#if DEBUG
                    , binding.IsIgnored
                        ? $"column{binding.Index}_ignored"
                        : $"column{binding.Index}_{binding.Type.Name}"
#endif
                    );
            }

            return array;
        }

        NewExpression GetObjectInitialization()
        {
            if (!bc.HasConstructorParameters)
                return Expression.New(typeof(TResult));

            var ctorParameters = bc.ConstructorParameters;
            var result = new ReadOnlyCollectionBuilder<Expression>(ctorParameters.Length);

            foreach (var (bindingOrNull, parameter) in ctorParameters)
            {
                Debug.Assert(bindingOrNull.HasValue || parameter.HasDefaultValue);

                Expression? parameterExpression;

                if (bindingOrNull is CsvBinding binding)
                {
                    parameterExpression = parameters[binding.Index];
                }
                else if (parameter.DefaultValue is not null)
                {
                    parameterExpression = Expression.Constant(parameter.DefaultValue, parameter.ParameterType);
                }
                else
                {
                    // DefaultValue is either not retrievable (default struct) or is null, applicable for scenarios like:
                    //  string? s = null
                    //  int? i = null
                    //  DateTime date = default
                    // In all of these cases we can just use default(T)
                    parameterExpression = Expression.Default(parameter.ParameterType);
                }

                result.Add(parameterExpression);
            }

            return Expression.New(bc.Constructor, result);
        }

        Expression GetExpressionBody()
        {
            if (!bc.HasMemberInitializers)
                return newExpr;

            var memberBindings = bc.MemberBindings;
            var result = new ReadOnlyCollectionBuilder<MemberBinding>(memberBindings.Length);

            foreach (var binding in memberBindings)
            {
                result.Add(Expression.Bind(binding.Member, parameters[binding.Index]));
            }

            return Expression.MemberInit(newExpr, result);
        }
    }
}
