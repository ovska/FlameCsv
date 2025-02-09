using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reflection;

namespace FlameCsv.Runtime;

[RUF(Messages.Reflection)]
[RDC(Messages.DynamicCode)]
internal sealed class ExpressionDelegateGenerator<T> : DelegateGenerator<T> where T : unmanaged, IBinaryInteger<T>
{
    public static readonly ExpressionDelegateGenerator<T> Instance = new();

    protected override Func<object[], IMaterializer<T, TResult>> GetMaterializerInit<[DAM(Messages.Ctors)] TResult>(
        CsvBindingCollection<TResult> bc)
    {
        ConstructorInfo ctor = Materializer<T>.GetConstructor(bc.Bindings);

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
        return lambda.CompileLambda<Func<object[], IMaterializer<T, TResult>>>(throwIfClosure: true);
    }

    protected override Delegate GetValueFactory<[DAM(Messages.Ctors)] TResult>(CsvBindingCollection<TResult> bc)
    {
        ReadOnlyCollectionBuilder<ParameterExpression> parameters = GetParametersByBindingIndex();
        NewExpression newExpr = GetObjectInitialization();
        Expression body = GetExpressionBody();
        return Expression.Lambda(body, parameters).CompileLambda<Delegate>(throwIfClosure: true);

        ReadOnlyCollectionBuilder<ParameterExpression> GetParametersByBindingIndex()
        {
            var builder = new ReadOnlyCollectionBuilder<ParameterExpression>(bc.Bindings.Length);

            foreach (var binding in bc.Bindings)
            {
                var param = Expression.Parameter(
                    binding.Type,
                    Debugger.IsAttached ? $"field_{binding.Index}_{binding.DisplayName}" : null);
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
                // TODO: get full binding collection for proxy type
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

            // TODO: figure out if we can get rid of this and use LightExpression
            return Expression.MemberInit(newExpr, result);
        }
    }
}
