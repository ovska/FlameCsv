using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using FlameCsv.Reading;

namespace FlameCsv.Runtime;

[RequiresUnreferencedCode(Messages.CompiledExpressions)]
[RequiresDynamicCode(Messages.CompiledExpressions)]
internal sealed class ExpressionDelegateGenerator<T> : DelegateGenerator<T> where T : unmanaged, IEquatable<T>
{
    protected override Func<object[], IMaterializer<T, TResult>> GetMaterializerInit<[DynamicallyAccessedMembers(Messages.Ctors)] TResult>(CsvBindingCollection<TResult> bc)
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

    protected override Delegate GetValueFactory<[DynamicallyAccessedMembers(Messages.Ctors)] TResult>(CsvBindingCollection<TResult> bc)
    {
        ParameterExpression[] parameters = GetParametersByBindingIndex();
        NewExpression newExpr = GetObjectInitialization();
        Expression body = GetExpressionBody();
        return Expression.Lambda(body, parameters).CompileLambda<Delegate>(throwIfClosure: true);

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
