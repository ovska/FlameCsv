using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Parsers;
using FlameCsv.Runtime;

namespace FlameCsv.Extensions;

internal static partial class CsvReadingExtensions
{
    private delegate IMaterializer<T, TResult> MaterializerFactory<T, TResult>(CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>;

    private static class CachedFactory<T, TResult> where T : unmanaged, IEquatable<T>
    {
        public static MaterializerFactory<T, TResult>? Value;
    }

    /// <summary>
    /// Creates a materializer from the bindings.
    /// </summary>
    public static IMaterializer<T, TResult> CreateMaterializerFrom<T, TResult>(
        this CsvReaderOptions<T> options,
        CsvBindingCollection<TResult> bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        return GetMaterializerFactory<T, TResult>(bindingCollection)(options);
    }

    /// <summary>
    /// Binds the options using built-in or index binding.
    /// </summary>
    public static IMaterializer<T, TResult> GetMaterializer<T, TResult>(this CsvReaderOptions<T> options)
        where T : unmanaged, IEquatable<T>
    {
        MaterializerFactory<T, TResult>? factory = CachedFactory<T, TResult>.Value;

        if (factory is null)
        {
            if (TryGetTupleBindings<T, TResult>(out var bindings) ||
                IndexAttributeBinder<TResult>.TryGetBindings(out bindings))
            {
                factory = GetMaterializerFactory<T, TResult>(bindings);
            }
            else
            {
                // Don't cache nulls since its unlikely they will be attempted many times
                throw new CsvBindingException<TResult>(
                    $"Headerless CSV could not be bound to {typeof(TResult)}, since the type had no " +
                    "[CsvIndex]-attributes and no built-in configuration.");
            }

            factory =
                Interlocked.CompareExchange(ref CachedFactory<T, TResult>.Value, factory, null)
                ?? factory;
        }

        return factory(options);
    }

    private static MaterializerFactory<T, TResult> GetMaterializerFactory<T, TResult>(
        CsvBindingCollection<TResult> bindingCollection)
        where T : unmanaged, IEquatable<T>
    {
        return GetMaterializerFactory<T, TResult>(bindingCollection, GetValueFactory(bindingCollection));
    }

    /// <summary>
    /// Creates the state object using the bindings and <typeparamref name="TResult"/> type parameter.
    /// </summary>
    private static MaterializerFactory<T, TResult> GetMaterializerFactory<T, TResult>(
        CsvBindingCollection<TResult> bindingCollection,
        Delegate valueFactory)
        where T : unmanaged, IEquatable<T>
    {
        ConstructorInfo ctor = Materializer<T>.GetConstructor(bindingCollection.Bindings);

        var objArrParam = Expression.Parameter(typeof(object[]), "args");

        // new Materializer(args[0], args[1], args[2], ...))
        var ctorInvoke = Expression.New(
            ctor,
            ctor.GetParameters()
                .Select(
                    (p, i) => Expression.Convert(
                        Expression.ArrayAccess(objArrParam, Expression.Constant(i)),
                        p.ParameterType)));
        var lambda = Expression.Lambda<Func<object[], IMaterializer<T, TResult>>>(
            Expression.Convert(ctorInvoke, typeof(IMaterializer<T, TResult>)),
            objArrParam);
        var materializerFactory = lambda.CompileLambda<Func<object[], IMaterializer<T, TResult>>>();

        return CreateMaterializerImpl;

        IMaterializer<T, TResult> CreateMaterializerImpl(CsvReaderOptions<T> options)
        {
            var bindings = bindingCollection.Bindings;

            object[] materializerCtorArgs = new object[bindings.Length + 1];
            materializerCtorArgs[0] = valueFactory;

            for (int i = 0; i < bindings.Length; i++)
            {
                materializerCtorArgs[i + 1] = ResolveParser(bindings[i], options);
            }

            return materializerFactory(materializerCtorArgs);
        }

        static ICsvParser<T> ResolveParser(CsvBinding<TResult> binding, CsvReaderOptions<T> options)
        {
            if (binding.IsIgnored)
            {
                return IgnoredColumnParser<T>.Instance;
            }

            if (binding.TryGetAttribute<CsvParserOverrideAttribute>(out var @override))
            {
                return @override.CreateParser(binding.Type, options);
            }

            return options.GetParser(binding.Type);
        }
    }

    private static Delegate GetValueFactory<TResult>(CsvBindingCollection<TResult> bc)
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

        // returns object initialization expr, either new T()
        // -- or --
        // if there are ctor parameters: new T(arg0, arg1)
        NewExpression GetObjectInitialization()
        {
            if (!bc.HasConstructorParameters)
                return Expression.New(typeof(TResult));

            var ctorParameters = bc.ConstructorParameters;
            var result = new ReadOnlyCollectionBuilder<Expression>(ctorParameters.Length);

            foreach (var (binding, parameter) in ctorParameters)
            {
                Debug.Assert(binding is not null || parameter.HasDefaultValue);

                Expression? parameterExpression;

                if (binding is not null)
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

        // returns either the new T(arg0, arg1) -expression
        // -- or -- 
        // member initialization: new T(arg0, arg1) { Prop = arg1 }
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
