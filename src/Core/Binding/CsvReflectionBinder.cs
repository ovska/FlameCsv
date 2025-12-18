using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using FastExpressionCompiler.LightExpression;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reflection;
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
        ImmutableArray<string> headerFields
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        var configuration = AttributeConfiguration.GetFor<TValue>(write: false);
        List<CsvBinding<TValue>> foundBindings = new(headerFields.Length);

        bool matchedAny = false;
        StringComparison comparison = options.IgnoreHeaderCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (string field in headerFields.AsSpan())
        {
            int index = foundBindings.Count;

            CsvBinding<TValue>? binding = null;

            foreach (ref readonly var data in configuration.Value)
            {
                if (data.Ignored)
                    continue;

                bool match = string.Equals(data.Name, field, comparison);

                for (int i = 0; !match && i < data.Aliases.Length; i++)
                {
                    match = string.Equals(data.Aliases[i], field, comparison);
                }

                if (match)
                {
                    binding = CsvBinding.FromBindingData<TValue>(index, in data);
                    matchedAny = true;
                    break;
                }
            }

            if (binding is null && !options.IgnoreUnmatchedHeaders)
            {
                throw new CsvBindingException(
                    $"Could not bind header '{field}' at index {index} to type {typeof(TValue).FullName}"
                )
                {
                    TargetType = typeof(TValue),
                    Headers = headerFields,
                };
            }

            matchedAny |= binding is not null;

            foundBindings.Add(binding ?? CsvBinding.Ignore<TValue>(index: foundBindings.Count));
        }

        if (!matchedAny)
        {
            throw new CsvBindingException("No bindings matched.")
            {
                TargetType = typeof(TValue),
                Headers = headerFields,
            };
        }

        return new CsvBindingCollection<TValue>(foundBindings, write: false, options.IgnoreDuplicateHeaders);
    }

    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    private protected static IDematerializer<T, TValue> Create<T, [DAM(Messages.ReflectionBound)] TValue>(
        CsvOptions<T> options
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        CsvBindingCollection<TValue>? bindingCollection;

        if (options.HasHeader)
        {
            bindingCollection = GetWriteHeaders<T, TValue>(options);
        }
        else if (
            !MaterializerExtensions.TryGetTupleBindings(options, write: true, out bindingCollection)
            && !IndexAttributeBinder<TValue>.TryGetBindings(write: true, out bindingCollection)
        )
        {
            throw new CsvBindingException(
                $"Headerless CSV could not be written for {typeof(TValue)} since the type had no [CsvIndex]-attributes."
            )
            {
                TargetType = typeof(TValue),
            };
        }

        var bindings = bindingCollection.MemberBindings;
        var ctor = Dematerializer<T>.GetConstructor(bindings);

        var parameters = new object[bindings.Length + 2];
        parameters[0] = bindingCollection;
        parameters[1] = options;

        var valueParam = Expression.Parameter(typeof(TValue), "obj");

        for (int i = 0; i < bindings.Length; i++)
        {
            MemberExpression memberExpression = bindings[i].Member.GetAsMemberExpression(valueParam);
            var lambda = Expression.Lambda(memberExpression, valueParam);
            parameters[i + 2] = lambda.CompileLambda<Delegate>(throwIfClosure: false);
        }

        IDematerializer<T, TValue> dematerializer = (IDematerializer<T, TValue>)ctor.Invoke(parameters);
        return dematerializer;
    }

    [RDC(Messages.Reflection)]
    private static CsvBindingCollection<TValue> GetWriteHeaders<T, [DAM(Messages.ReflectionBound)] TValue>(
        CsvOptions<T> options
    )
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

        return new CsvBindingCollection<TValue>(result, write: true, ignoreDuplicates: options.IgnoreDuplicateHeaders);
    }
}

/// <summary>
/// Binds type members and constructor parameters to CSV fields using reflection.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public sealed class CsvReflectionBinder<T> : CsvReflectionBinder, ICsvTypeBinder<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvOptions<T> _options;

    /// <summary>
    /// Creates an instance of <see cref="CsvReflectionBinder{T}"/>.
    /// </summary>
    public CsvReflectionBinder(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Caches the return values based on the options and headers.
    /// </remarks>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public IMaterializer<T, TValue> GetMaterializer<[DAM(Messages.ReflectionBound)] TValue>(
        ImmutableArray<string> headers
    )
    {
        if (headers.IsDefaultOrEmpty)
            Throw.DefaultOrEmptyImmutableArray(nameof(headers));

        return _options.GetMaterializer(
            headers,
            static (options, headers) =>
            {
                var bindings = GetReadBindings<T, TValue>(options, headers);
                return options.CreateMaterializerFrom(bindings);
            }
        );
    }

    /// <inheritdoc />
    /// <remarks>
    /// Caches the return values based on the options.
    /// </remarks>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public IMaterializer<T, TValue> GetMaterializer<[DAM(Messages.ReflectionBound)] TValue>()
    {
        return _options.GetMaterializer([], static (options, _) => options.GetMaterializerNoHeader<T, TValue>());
    }

    /// <inheritdoc />
    /// <remarks>
    /// Caches the return values based on the options.
    /// </remarks>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public IDematerializer<T, TValue> GetDematerializer<[DAM(Messages.ReflectionBound)] TValue>()
    {
        return _options.GetDematerializer(static options => Create<T, TValue>(options));
    }
}
