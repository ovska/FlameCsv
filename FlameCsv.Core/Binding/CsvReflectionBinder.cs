using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Helpers;
using FlameCsv.Binding.Attributes;
using FlameCsv.Binding.Internal;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Reflection;
using FlameCsv.Runtime;
using FlameCsv.Utilities;
using FlameCsv.Writing;

namespace FlameCsv.Binding;

/// <summary>
/// Internal implementation detail.
/// </summary>
public abstract class CsvReflectionBinder
{
    private protected sealed class CacheKey : IEquatable<CacheKey>
    {
        public static bool CanCache(int headersLength) => headersLength <= StringScratch.MaxLength;

        private readonly WeakReference<object> _options;
        private readonly Type _targetType;
        private readonly bool _ignoreUnmatched;
        private readonly int _length;
        private StringScratch _headers;

        public CacheKey(object options, Type targetType, bool ignoreUnmatched, ReadOnlySpan<string> headers)
        {
            Debug.Assert(headers.Length <= StringScratch.MaxLength);

            _options = new(options);
            _targetType = targetType;
            _ignoreUnmatched = ignoreUnmatched;
            _length = headers.Length;
            _headers = default;
            headers.CopyTo(_headers!);
        }

        public bool Equals(CacheKey? other)
        {
            return
                other is not null &&
                _length == other._length &&
                _ignoreUnmatched == other._ignoreUnmatched &&
                _targetType == other._targetType &&
                _headers.AsSpan(_length).SequenceEqual(other._headers.AsSpan(other._length)) &&
                _options.TryGetTarget(out object? target) &&
                other._options.TryGetTarget(out object? otherTarget) &&
                ReferenceEquals(target, otherTarget);
        }

        public override bool Equals(object? obj) => Equals(obj as CacheKey);

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode()
            => HashCode.Combine(
                _targetType.GetHashCode(),
                _options.TryGetTarget(out object? target) ? (target?.GetHashCode() ?? 0) : 0,
                _ignoreUnmatched.GetHashCode(),
                _length,
                HashCode<string>.Combine(_headers.AsSpan(_length)));
    }

    private protected static readonly TrimmingCache<CacheKey, object>
        _materializerCache = new(EqualityComparer<CacheKey>.Default);

    private static readonly TrimmingCache<object, object> _dematerializerCache = [];

    private protected static CsvBindingCollection<TValue> GetReadBindings<T, [DAM(Messages.ReflectionBound)] TValue>(
        CsvOptions<T> options,
        ReadOnlySpan<string> headerFields,
        bool ignoreUnmatched)
        where T : unmanaged, IBinaryInteger<T>
    {
        HeaderData headerData = GetHeaderDataFor<TValue>(write: false);
        ReadOnlySpan<string> ignoredValues = headerData.IgnoredValues;
        List<CsvBinding<TValue>> foundBindings = new(headerFields.Length);

        foreach (var field in headerFields)
        {
            int index = foundBindings.Count;

            CsvBinding<TValue>? binding = null;

            foreach (var value in ignoredValues)
            {
                if (options.Comparer.Equals(value, field))
                {
                    binding = CsvBinding.Ignore<TValue>(index);
                    break;
                }
            }

            if (binding is null)
            {
                foreach (ref readonly var candidate in headerData.Candidates)
                {
                    if (options.Comparer.Equals(candidate.Value, field))
                    {
                        binding = CsvBinding.FromHeaderBinding<TValue>(index, in candidate);
                        break;
                    }
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
        if (_dematerializerCache.TryGetValue(options, out var dematerializer))
        {
            return (IDematerializer<T, TValue>)dematerializer;
        }

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

        IDematerializer<T, TValue> created = (IDematerializer<T, TValue>)ctor.Invoke(parameters);
        _dematerializerCache.Add(options, created);
        return created;
    }

    private static CsvBindingCollection<TValue> GetWriteHeaders<T, [DAM(Messages.ReflectionBound)] TValue>()
        where T : unmanaged, IBinaryInteger<T>
    {
        var candidates = GetHeaderDataFor<TValue>(write: true).Candidates;

        List<CsvBinding<TValue>> result = new(candidates.Length);
        HashSet<object> handledMembers = [];
        int index = 0;

        foreach (var candidate in candidates)
        {
            Debug.Assert(candidate.Target is not ParameterInfo);

            if (handledMembers.Add(candidate.Target))
                result.Add(CsvBinding.FromHeaderBinding<TValue>(index++, in candidate));
        }

        return new CsvBindingCollection<TValue>(result, write: true);
    }

    internal static readonly TrimmingCache<Type, HeaderDataEntry> Cache = [];

    internal sealed class HeaderData(string[]? ignoredValues, List<HeaderBindingCandidate> candidates)
    {
        public ReadOnlySpan<string> IgnoredValues => ignoredValues;
        public ReadOnlySpan<HeaderBindingCandidate> Candidates => candidates.AsSpan();
    }

    internal sealed class HeaderDataEntry
    {
        public HeaderDataEntry(Func<HeaderData> read, Func<HeaderData> write)
        {
            _read = new(read);
            _write = new(write);
        }

        public HeaderData Read => _read.Value;
        public HeaderData Write => _write.Value;

        private readonly Lazy<HeaderData> _read;
        private readonly Lazy<HeaderData> _write;
    }

    /// <summary>
    /// Returns members of <typeparamref name="TValue"/> that can be used for binding.
    /// </summary>
    private protected static HeaderData GetHeaderDataFor<[DAM(Messages.ReflectionBound)] TValue>(bool write)
    {
        if (!Cache.TryGetValue(typeof(TValue), out var entry))
        {
            entry = new HeaderDataEntry(
                static () => GetHeaderDataCore<TValue>(write: false),
                static () => GetHeaderDataCore<TValue>(write: true));
            Cache.Add(typeof(TValue), entry);
        }

        return write ? entry.Write : entry.Read;
    }

    private static HeaderData GetHeaderDataCore<[DAM(Messages.ReflectionBound)] TValue>(bool write)
    {
        List<HeaderBindingCandidate> candidates = [];
        CsvTypeAttribute? typeAttribute = null;

        foreach (var member in CsvTypeInfo.Members<TValue>())
        {
            if (!write && member.IsReadOnly)
                continue;

            bool found = false;

            foreach (var attribute in member.Attributes)
            {
                if (attribute is not CsvFieldAttribute attr)
                {
                    continue;
                }

                found = true;

                if (attr.IsIgnored)
                {
                    // TODO: what to do here?
                    continue;
                }

                if (attr.Headers is { Length: > 0 })
                {
                    foreach (var value in attr.Headers)
                    {
                        candidates.Add(new HeaderBindingCandidate(value, member.Value, attr.Order, attr.IsRequired));
                    }
                }
                else
                {
                    candidates.Add(
                        new HeaderBindingCandidate(member.Value.Name, member.Value, attr.Order, attr.IsRequired));
                }
            }

            if (!found)
            {
                candidates.Add(new HeaderBindingCandidate(member.Value.Name, member.Value, 0, isRequired: false));
            }
        }

        foreach (var attribute in CsvTypeInfo.Attributes<TValue>())
        {
            if (attribute is CsvTypeAttribute typeAttr)
            {
                typeAttribute = typeAttr;
                continue;
            }

            if (attribute is not CsvTypeFieldAttribute attr) continue;

            if (attr.IsParameter)
            {
                if (write) continue;

                var parameter = CsvTypeInfo.GetParameter<TValue>(attr.MemberName).Value;

                foreach (var value in attr.Headers)
                {
                    candidates.Add(new HeaderBindingCandidate(value, parameter, attr.Order, attr.IsRequired));
                }
            }
            else
            {
                var member = CsvTypeInfo.GetPropertyOrField<TValue>(attr.MemberName).Value;

                foreach (var value in attr.Headers)
                {
                    candidates.Add(new HeaderBindingCandidate(value, member, attr.Order, attr.IsRequired));
                }
            }
        }

        foreach (var parameter in !write ? CsvTypeInfo.ConstructorParameters<TValue>() : default)
        {
            CsvFieldAttribute? attr = null;

            foreach (var attribute in parameter.Attributes)
            {
                if (attribute is CsvFieldAttribute match)
                {
                    attr = match;
                    break;
                }
            }

            if (attr is not null)
            {
                if (attr.IsIgnored)
                {
                    // TODO: what to do here?
                    continue;
                }

                if (attr.Headers.Length == 0)
                {
                    candidates.Add(
                        new HeaderBindingCandidate(
                            parameter.Value.Name!,
                            parameter.Value,
                            attr.Order,
                            attr.IsRequired));
                }
                else
                {
                    foreach (var value in attr.Headers)
                    {
                        candidates.Add(new HeaderBindingCandidate(value, parameter.Value, attr.Order, attr.IsRequired));
                    }
                }
            }
            else
            {
                candidates.Add(
                    new HeaderBindingCandidate(
                        parameter.Value.Name!,
                        parameter.Value,
                        0,
                        isRequired: false));
            }

            // hack for records: remove init-only properties with the same name and type
            // as a parameter
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                HeaderBindingCandidate existing = candidates[i];

                if (existing.Target is PropertyInfo prop &&
                    prop.Name == parameter.Value.Name &&
                    prop.PropertyType == parameter.Value.ParameterType &&
                    prop.SetMethod is { ReturnParameter: var rp } &&
                    rp.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                {
                    candidates.RemoveAt(i);
                }
            }
        }

        candidates.AsSpan().Sort(); // sorted by Order
        return new HeaderData(typeAttribute?.IgnoredHeaders, candidates);
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
        ReadOnlySpan<string> headers)
    {
        if (CacheKey.CanCache(headers.Length))
        {
            CacheKey key = new(_options, typeof(TValue), IgnoreUnmatched, headers);

            if (_materializerCache.TryGetValue(key, out var cached))
            {
                return (IMaterializer<T, TValue>)cached;
            }

            var materializer
                = _options.CreateMaterializerFrom(GetReadBindings<T, TValue>(_options, headers, IgnoreUnmatched));
            _materializerCache.Add(key, materializer);
            return materializer;
        }

        return _options.CreateMaterializerFrom(GetReadBindings<T, TValue>(_options, headers, IgnoreUnmatched));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Caches the return values based on the options.
    /// </remarks>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public IMaterializer<T, TValue> GetMaterializer<[DAM(Messages.ReflectionBound)] TValue>()
    {
        return _options.GetMaterializer<T, TValue>();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Caches the return values based on the options.
    /// </remarks>
    [RUF(Messages.Reflection)]
    [RDC(Messages.DynamicCode)]
    public IDematerializer<T, TValue> GetDematerializer<[DAM(Messages.ReflectionBound)] TValue>()
    {
        return Create<T, TValue>(_options);
    }
}
