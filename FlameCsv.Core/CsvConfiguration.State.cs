using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Runtime;

namespace FlameCsv;

public sealed partial class CsvConfiguration<T>
{
    internal ICsvRowState<T, TResult> BindToState<TResult>()
    {
        if (BindingProvider.TryGetBindings<TResult>(out var bindings))
            return CreateState(bindings);

        throw new CsvBindingException();
    }

    /// <summary>
    /// Creates the state object using the bindings and <typeparamref name="TResult"/> type parameter.
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal ICsvRowState<T, TResult> CreateState<TResult>(CsvBindingCollection<TResult> bindingCollection)
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
        object[] rowStateConstructorArgs = new object[bindings.Length + 1];
        rowStateConstructorArgs[0] = factoryGenerator.Invoke(null, factoryGeneratorParameters)
            ?? throw new InvalidOperationException($"Failed to create factory func from {factoryGenerator}");

        for (int i = 0; i < bindings.Length; i++)
        {
            var @override = GetOverride(bindings[i]);

            if (@override is not null)
            {
                rowStateConstructorArgs[i + 1] = @override.CreateParser(bindings[i], this);
            }
            else
            {
                rowStateConstructorArgs[i + 1] = GetParser(bindings[i].Type);
            }
        }

        ConstructorInfo ctor = CsvRowState.GetConstructor<T, TResult>(bindings.Select(b => b.Type));
        return (ICsvRowState<T, TResult>)ctor.Invoke(rowStateConstructorArgs);

        static ICsvParserOverride? GetOverride(in CsvBinding binding)
        {
            ICsvParserOverride? found = null;

            foreach (var attribute in binding.Member.GetCachedCustomAttributes())
            {
                if (attribute is ICsvParserOverride @override)
                {
                    if (found is not null)
                        throw new CsvBindingException(typeof(TResult), binding, found, @override);

                    found = @override;
                }
            }

            return found;
        }
    }
}
