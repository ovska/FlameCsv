using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using FastExpressionCompiler.LightExpression;
using FlameCsv.Binding;
using FlameCsv.Reading;

namespace FlameCsv.Reflection;

[RUF(Messages.Reflection)]
[RDC(Messages.DynamicCode)]
internal static class ExpressionDelegateGenerator<T>
    where T : unmanaged, IBinaryInteger<T>
{
    public static Func<CsvOptions<T>, IMaterializer<T, TResult>> GetMaterializerFactory<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc
    )
    {
        ArgumentNullException.ThrowIfNull(bc);
        var materializerFactory = GetMaterializerInit(bc, out Type factoryType);
        var valueFactory = GetValueFactory(bc, factoryType);
        return options => materializerFactory.Invoke([valueFactory, bc, options]);
    }

    private static Func<object[], IMaterializer<T, TResult>> GetMaterializerInit<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc,
        out Type factoryType
    )
    {
        ConstructorInfo ctor = Materializer<T>.GetConstructor(bc.Bindings);

        var objArrParam = Expression.Parameter(typeof(object[]), "args");

        var ctorParams = ctor.GetParameters();
        factoryType = ctorParams[0].ParameterType;

        ReadOnlyCollectionBuilder<Expression> parameters = new(ctorParams.Length);

        for (int i = 0; i < ctorParams.Length; i++)
        {
            parameters.Add(
                Expression.Convert(
                    Expression.ArrayAccess(objArrParam, Expression.Constant(i)),
                    ctorParams[i].ParameterType
                )
            );
        }

        // new Materializer(args[0], args[1], args[2], ...))
        var ctorInvoke = Expression.New(ctor, parameters);
        var lambda = Expression.Lambda<Func<object[], IMaterializer<T, TResult>>>(
            Expression.Convert<IMaterializer<T, TResult>>(ctorInvoke),
            objArrParam
        );

        return lambda.CompileLambda<Func<object[], IMaterializer<T, TResult>>>(throwIfClosure: true);
    }

    private static Delegate GetValueFactory<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc,
        Type factoryType
    )
    {
        ReadOnlyCollectionBuilder<ParameterExpression> parameters = GetParametersByBindingIndex();
        NewExpression newExpr = GetObjectInitialization();
        Expression body = GetExpressionBody();

        return Expression
            .Lambda(factoryType, body, parameters, typeof(TResult))
            .CompileLambda<Delegate>(throwIfClosure: true);

        ReadOnlyCollectionBuilder<ParameterExpression> GetParametersByBindingIndex()
        {
            var builder = new ReadOnlyCollectionBuilder<ParameterExpression>(bc.Bindings.Length);

            foreach (var binding in bc.Bindings)
            {
                var param = Expression.Parameter(
                    binding.Type,
                    Debugger.IsAttached ? $"field_{binding.Index}_{binding.DisplayName}" : null
                );
                builder.Add(param);
            }

            return builder;
        }

        // returns object initialization expr, either new T()
        // -- or --
        // if there are ctor parameters: new T(arg0, arg1)
        NewExpression GetObjectInitialization()
        {
            if (CsvTypeInfo<TResult>.Value.Proxy is not null)
            {
                // TODO: get full binding collection for proxy type?
                return Expression.New(CsvTypeInfo<TResult>.Value.Proxy!.Type);
            }

            if (!bc.HasConstructorParameters)
                return Expression.New(typeof(TResult));

            var ctorParameters = bc.ConstructorParameters;
            var result = new ReadOnlyCollectionBuilder<Expression>(ctorParameters.Length);

            foreach ((ParameterCsvBinding<TResult>? binding, ParameterInfo parameter) in ctorParameters)
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

        // returns either just the new T(arg0, arg1) -expression
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
