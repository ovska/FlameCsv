using FlameCsv.Converters;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
using JetBrains.Annotations;
#if DEBUG
using Unsafe = FlameCsv.Extensions.DebugUnsafe
#else
using Unsafe = System.Runtime.CompilerServices.Unsafe
#endif
    ;

namespace FlameCsv;

partial class CsvOptions<T>
{
    /// <summary>
    /// Collection of all converters and factories of the options instance, not including the built-in converters
    /// (see <see cref="UseDefaultConverters"/>).
    /// </summary>
    /// <remarks>
    /// Modifying the collection after the options-instance is used (<see cref="IsReadOnly"/> is <see langword="true"/>)
    /// results in an exception.
    /// </remarks>
    public IList<CsvConverter<T>> Converters
    {
        get
        {
            var local = _converters;

            if (local is not null)
            {
                return local;
            }

            var result = IsReadOnly
                ? SealableList<CsvConverter<T>>.Empty
                : new SealableList<CsvConverter<T>>(this, null);

            return Interlocked.CompareExchange(ref _converters, result, null) ?? _converters;
        }
    }

    private SealableList<CsvConverter<T>>? _converters;

    /// <summary>
    /// Contains cached converters for types that have been requested with <see cref="GetConverter(Type)"/>.
    /// </summary>
    /// <seealso cref="MaxConverterCacheSize"/>
    protected internal readonly ConcurrentDictionary<Type, CsvConverter<T>> _converterCache
        = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Maximum number of converters cached internally by the options instance before the cache is cleared.
    /// For converters by type, for example, with <see cref="GetConverter(Type)"/>.
    /// Default is <see cref="int.MaxValue"/>.
    /// </summary>
    protected int MaxConverterCacheSize
    {
        get => _maxConverterCacheSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);
            _maxConverterCacheSize = value;
            CheckConverterCacheSize();
        }
    }

    private int _maxConverterCacheSize = int.MaxValue;

    /// <summary>
    /// Returns a converter for <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">Type to convert</typeparam>
    /// <exception cref="CsvConverterMissingException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    public CsvConverter<T, TResult> GetConverter<TResult>()
    {
        if (typeof(TResult) == typeof(CsvIgnored))
        {
            return CsvIgnored.Converter<T, TResult>();
        }

        return (CsvConverter<T, TResult>)GetConverter(typeof(TResult));
    }

    /// <summary>
    /// Returns a converter for <paramref name="resultType"/>.
    /// </summary>
    /// <remarks>Never returns a <see cref="CsvConverterFactory{T}"/></remarks>
    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    public CsvConverter<T> GetConverter(Type resultType)
    {
        if (!TryGetExistingOrCustomConverter(resultType, out CsvConverter<T>? converter, out bool created) &&
            UseDefaultConverters)
        {
            if (TryCreateDefaultConverter(resultType, out var builtin))
            {
                Debug.Assert(builtin.CanConvert(resultType), $"Invalid builtin converter {builtin} for {resultType}");
                Debug.Assert(
                    builtin is not CsvConverterFactory<T>,
                    $"{resultType} default converter returned a factory");
                converter = builtin;
                created = true;
            }
            else if (NullableConverterFactory<T>.Instance.CanConvert(resultType))
            {
                converter = NullableConverterFactory<T>.Instance.Create(resultType, this);
            }
        }

        if (converter is null) CsvConverterMissingException.Throw(resultType);

        if (created)
        {
            CheckConverterCacheSize();

            // ensure we return the same instance that was cached
            return _converterCache.GetOrAdd(resultType, converter);
        }

        return converter;
    }

    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    private bool TryGetExistingOrCustomConverter(
        Type resultType,
        [NotNullWhen(true)] out CsvConverter<T>? converter,
        out bool created)
    {
        ArgumentNullException.ThrowIfNull(resultType);
        MakeReadOnly();

        if (_converterCache.TryGetValue(resultType, out var cached))
        {
            Debug.Assert(cached.CanConvert(resultType));
            converter = cached;
            created = false;
            return true;
        }

        var local = _converters;

        // null of the Converters-property is never accessed (no custom converters)
        if (local is not null)
        {
            ReadOnlySpan<CsvConverter<T>> converters = local.Span;

            // Read converters in reverse order so parser added last has the highest priority
            for (int i = converters.Length - 1; i >= 0; i--)
            {
                if (converters[i].CanConvert(resultType))
                {
                    converter = converters[i].GetOrCreateConverter(resultType, this);
                    created = true;
                    return true;
                }
            }
        }

        converter = null;
        created = false;
        return false;
    }

    private bool TryGetExistingOrCustomConverter<TValue>(
        [NotNullWhen(true)] out CsvConverter<T, TValue>? converter)
    {
        MakeReadOnly();

        if (_converterCache.TryGetValue(typeof(TValue), out var cached) &&
            cached is CsvConverter<T, TValue> casted)
        {
            converter = casted;
            return true;
        }

        var local = _converters;

        // null of the Converters-property is never accessed (no custom converters)
        if (local is not null)
        {
            ReadOnlySpan<CsvConverter<T>> converters = local.Span;

            // Read converters in reverse order so parser added last has the highest priority
            for (int i = converters.Length - 1; i >= 0; i--)
            {
                if (converters[i] is CsvConverter<T, TValue> converterOfT)
                {
                    _converterCache.TryAdd(typeof(TValue), converter = converterOfT);
                    return true;
                }
            }
        }

        converter = null;
        return false;
    }

    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    private bool TryCreateDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<T>? converter)
    {
        if (typeof(T) == typeof(char))
        {
            CsvOptions<char> options = Unsafe.As<CsvOptions<char>>(this);

            CsvConverter<char>? result = DefaultConverters.Text.Value.TryGetValue(type, out var factory)
                ? factory(options)
                : DefaultConverterFactories.TryCreateChar(type, options);

            if (result != null)
            {
                converter = Unsafe.As<CsvConverter<T>>(result);
                return true;
            }
        }

        if (typeof(T) == typeof(byte))
        {
            CsvOptions<byte> options = Unsafe.As<CsvOptions<byte>>(this);

            CsvConverter<byte>? result = DefaultConverters.Utf8.Value.TryGetValue(type, out var factory)
                ? factory(options)
                : DefaultConverterFactories.TryCreateByte(type, options);

            if (result != null)
            {
                converter = Unsafe.As<CsvConverter<T>>(result);
                return true;
            }
        }

        converter = null;
        return false;
    }

    #region Source Generator

    /// <summary>
    /// Returns a converter for <typeparamref name="TValue"/>, either a configured one or from the factory.
    /// </summary>
    /// <remarks>
    /// This API is meant to be used by the source generator to produce trimming/AOT safe code.<br/>
    /// If a non-factory user defined converter is found, it is returned directly.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TValue> GetOrCreate<TValue>(
        [RequireStaticDelegate] Func<CsvOptions<T>, CsvConverter<T, TValue>> factory,
        bool canCache = false)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (TryGetExistingOrCustomConverter<TValue>(out CsvConverter<T, TValue>? converter))
        {
            return converter;
        }

        converter = factory(this);

        if (converter is null) InvalidConverter.Throw(factory, typeof(TValue));

        if (canCache)
        {
            CheckConverterCacheSize();
            _converterCache.TryAdd(typeof(TValue), converter);
        }

        return converter;
    }

    /// <summary>
    /// Returns a nullable converter, falling back to the built-in one if <see cref="UseDefaultConverters"/> is true.
    /// </summary>
    /// <remarks>
    /// This API is meant to be used by the source generator to produce trimming/AOT safe code.<br/>
    /// If a non-factory user defined converter is found, it is returned directly.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TValue?> GetOrCreateNullable<TValue>(
        [RequireStaticDelegate] Func<CsvOptions<T>, CsvConverter<T, TValue>> factory,
        bool canCache = false)
        where TValue : struct
    {
        if (TryGetExistingOrCustomConverter(out CsvConverter<T, TValue?>? converter))
        {
            return converter;
        }

        if (!UseDefaultConverters) CsvConverterMissingException.Throw(typeof(TValue?));

        CsvConverter<T, TValue> inner = GetOrCreate(factory);
        if (inner is null) InvalidConverter.Throw(factory, typeof(TValue));

        var result = TrimmableNullableConverter.Create(inner, GetNullToken(typeof(TValue)));

        if (canCache)
        {
            CheckConverterCacheSize();
            _converterCache.TryAdd(typeof(TValue?), result);
        }

        return result;
    }

    /// <summary>
    /// Returns an enum converter, falling back to the built-in one if <see cref="UseDefaultConverters"/> is true.
    /// </summary>
    /// <remarks>
    /// This API is meant to be used by the source generator to produce trimming/AOT safe code.<br/>
    /// If a non-factory user defined converter is found, it is returned directly.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TEnum> GetOrCreateEnum<TEnum>() where TEnum : struct, Enum
    {
        if (typeof(T) == typeof(char))
        {
            return (CsvConverter<T, TEnum>)(object)Unsafe
                .As<CsvOptions<char>>(this)
                .GetOrCreate(
                    static o =>
                    {
                        if (!o.UseDefaultConverters) CsvConverterMissingException.Throw(typeof(TEnum));
                        return new EnumTextConverter<TEnum>(o);
                    },
                    canCache: true);
        }

        if (typeof(T) == typeof(byte))
        {
            return (CsvConverter<T, TEnum>)(object)Unsafe
                .As<CsvOptions<byte>>(this)
                .GetOrCreate(
                    static o =>
                    {
                        if (!o.UseDefaultConverters) CsvConverterMissingException.Throw(typeof(TEnum));
                        return new EnumUtf8Converter<TEnum>(o);
                    },
                    canCache: true);
        }

        throw new NotSupportedException("Enum converters are only supported for char and byte");
    }

#endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckConverterCacheSize()
    {
        if (_converterCache.Count >= MaxConverterCacheSize) _converterCache.Clear();
    }
}

file static class InvalidConverter
{
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(object factory, Type toConvert)
    {
        throw new CsvConfigurationException(
            $"{factory.GetType().FullName} returned an invalid converter for {toConvert.FullName}");
    }
}

[RUF(Messages.ConverterFactories)]
file static class DefaultConverterFactories
{
    public static CsvConverter<char>? TryCreateChar(Type type, CsvOptions<char> options)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            if (EnumTextConverterFactory.Instance.CanConvert(type))
            {
                return EnumTextConverterFactory.Instance.Create(type, options);
            }

            if (SpanTextConverterFactory.Instance.CanConvert(type))
            {
                return SpanTextConverterFactory.Instance.Create(type, options);
            }
        }

        return null;
    }

    public static CsvConverter<byte>? TryCreateByte(Type type, CsvOptions<byte> options)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
        {
            if (EnumUtf8ConverterFactory.Instance.CanConvert(type))
            {
                return EnumUtf8ConverterFactory.Instance.Create(type, options);
            }

            if (SpanUtf8ConverterFactory.Instance.CanConvert(type))
            {
                return SpanUtf8ConverterFactory.Instance.Create(type, options);
            }
        }

        return null;
    }
}
