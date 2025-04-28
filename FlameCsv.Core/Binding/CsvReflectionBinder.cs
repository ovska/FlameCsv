using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using FastExpressionCompiler.LightExpression;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reflection;
using FlameCsv.Runtime;
using FlameCsv.Writing;

namespace FlameCsv.Binding;

/// <summary>
/// Internal implementation detail.
/// </summary>
public abstract class CsvReflectionBinder
{
    [RDC(Messages.Reflection)]
    private protected static CsvBindingCollection<TValue> GetReadBindings<T, [DAM(Messages.ReflectionBound)] TValue>(
        CsvOptions<T> options,
        ImmutableArray<string> headerFields,
        bool ignoreUnmatched)
        where T : unmanaged, IBinaryInteger<T>
    {
        var configuration = AttributeConfiguration.GetFor<TValue>(write: false);
        List<CsvBinding<TValue>> foundBindings = new(headerFields.Length);

        foreach (string field in headerFields)
        {
            int index = foundBindings.Count;

            CsvBinding<TValue>? binding = null;

            foreach (ref readonly var data in configuration.Value)
            {
                if (data.Ignored) continue;

                bool match = options.Comparer.Equals(data.Name, field);

                for (int i = 0; !match && i < data.Aliases.Length; i++)
                {
                    match = options.Comparer.Equals(data.Aliases[i], field);
                }

                if (match)
                {
                    binding = CsvBinding.FromBindingData<TValue>(index, in data);
                    break;
                }
            }

            if (binding is null && !ignoreUnmatched)
            {
                throw new CsvBindingException(
                    $"Could not bind header '{field}' at index {index} to type {typeof(TValue).FullName}");
            }

            foundBindings.Add(binding ?? CsvBinding.Ignore<TValue>(index: foundBindings.Count));
        }

        return new CsvBindingCollection<TValue>(foundBindings, write: false);
    }

    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    private protected static IDematerializer<T, TValue> Create<T, [DAM(Messages.ReflectionBound)] TValue>(
        CsvOptions<T> options)
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvBindingCollection<TValue>? bindingCollection;

        if (options.HasHeader)
        {
            bindingCollection = GetWriteHeaders<T, TValue>();
        }
        else if (!MaterializerExtensions.TryGetTupleBindings<T, TValue>(write: true, out bindingCollection) &&
                 !IndexAttributeBinder<TValue>.TryGetBindings(write: true, out bindingCollection))
        {
            throw new CsvBindingException<TValue>(
                $"Headerless CSV could not be written for {typeof(TValue)} since the type had no " +
                "[CsvIndex]-attributes.");
        }

        var bindings = bindingCollection.MemberBindings;
        var ctor = Dematerializer<T>.GetConstructor(bindings);

        var parameters = new object[bindings.Length + 2];
        parameters[0] = bindingCollection;
        parameters[1] = options;

        var valueParam = Expression.Parameter(typeof(TValue), "obj");

        for (int i = 0; i < bindings.Length; i++)
        {
            (MemberExpression memberExpression, _) = bindings[i].Member.GetAsMemberExpression(valueParam);
            var lambda = Expression.Lambda(memberExpression, valueParam);
            parameters[i + 2] = lambda.CompileLambda<Delegate>(throwIfClosure: false);
        }

        IDematerializer<T, TValue> dematerializer = (IDematerializer<T, TValue>)ctor.Invoke(parameters);
        return dematerializer;
    }

    [RDC(Messages.Reflection)]
    private static CsvBindingCollection<TValue> GetWriteHeaders<T, [DAM(Messages.ReflectionBound)] TValue>()
        where T : unmanaged, IBinaryInteger<T>
    {
        var candidates = AttributeConfiguration.GetFor<TValue>(write: true).Value;

        List<CsvBinding<TValue>> result = new(candidates.Length);
        HashSet<object> handledMembers = [];
        int index = 0;

        foreach (var candidate in candidates)
        {
            Debug.Assert(candidate.Target is not ParameterInfo);

            if (handledMembers.Add(candidate.Target))
                result.Add(CsvBinding.FromBindingData<TValue>(index++, in candidate));
        }

        return new CsvBindingCollection<TValue>(result, write: true);
    }
}

/// <summary>
/// Binds type members and constructor parameters to CSV fields using reflection.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public sealed class CsvReflectionBinder<T> : CsvReflectionBinder, ICsvTypeBinder<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Fields that could not be matched are ignored.
    /// </summary>
    public bool IgnoreUnmatched { get; }

    private readonly CsvOptions<T> _options;

    /// <summary>
    /// Creates an instance of <see cref="CsvReflectionBinder{T}"/>.
    /// </summary>
    public CsvReflectionBinder(CsvOptions<T> options, bool ignoreUnmatched)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        IgnoreUnmatched = ignoreUnmatched;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Caches the return values based on the options and headers.
    /// </remarks>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public IMaterializer<T, TValue> GetMaterializer<[DAM(Messages.ReflectionBound)] TValue>(
        ImmutableArray<string> headers)
    {
        Throw.IfDefaultOrEmpty(headers);

        return _options.GetMaterializer<TValue>(
            headers,
            IgnoreUnmatched,
            static (options, headers, ignoreUnmatched) =>
            {
                var bindings = GetReadBindings<T, TValue>(options, headers, ignoreUnmatched);
                return options.CreateMaterializerFrom(bindings);
            });
    }

    /// <inheritdoc />
    /// <remarks>
    /// Caches the return values based on the options.
    /// </remarks>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public IMaterializer<T, TValue> GetMaterializer<[DAM(Messages.ReflectionBound)] TValue>()
    {
        return _options.GetMaterializer<TValue>(
            [],
            false,
            static (options, _, _) => options.GetMaterializerNoHeader<T, TValue>());
    }

    /// <inheritoc />
    /// <remarks>
    /// Caches the return values based on the options.
    /// </remarks>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public IDematerializer<T, TValue> GetDematerializer<[DAM(Messages.ReflectionBound)] TValue>()
    {
        return _options.GetDematerializer<TValue>(static options => Create<T, TValue>(options));
    }
}
