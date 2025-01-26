using FlameCsv.Converters;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
            if (local is not null) return local;
            return Interlocked.CompareExchange(
                    ref _converters,
                    new SealableList<CsvConverter<T>>(this, defaultValues: null),
                    null) ??
                _converters;
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
    public CsvConverter<T, TResult> GetConverter<TResult>()
    {
        if (_converterCache.TryGetValue(typeof(TResult), out var cached))
        {
            return (CsvConverter<T, TResult>)cached;
        }

#pragma warning disable CA2263 // the generic overload calls this method anyway
        var converter = TryGetConverter(typeof(TResult));
#pragma warning restore CA2263

        if (converter is null)
        {
            CsvConverterMissingException.Throw(typeof(TResult));
        }

        return (CsvConverter<T, TResult>)converter;
    }

    /// <summary>
    /// Returns a converter for <paramref name="resultType"/>.
    /// </summary>
    /// <remarks>Never returns a <see cref="CsvConverterFactory{T}"/></remarks>
    /// <exception cref="CsvConverterMissingException"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvConverter<T> GetConverter(Type resultType)
    {
        ArgumentNullException.ThrowIfNull(resultType);

        if (_converterCache.TryGetValue(resultType, out var cached))
        {
            return cached;
        }

        var converter = TryGetConverter(resultType);

        if (converter is null)
        {
            CsvConverterMissingException.Throw(resultType);
        }

        return converter;
    }

    /// <summary>
    /// Returns a converter for <typeparamref name="TResult"/>, or null if not found.
    /// </summary>
    /// <remarks>Never returns a <see cref="CsvConverterFactory{T}"/></remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CsvConverter<T, TResult>? TryGetConverter<TResult>()
    {
        return (CsvConverter<T, TResult>?)TryGetConverter(typeof(TResult));
    }

    /// <summary>
    /// Returns a converter for <paramref name="resultType"/>, or null if there is none registered
    /// </summary>
    /// <remarks>Never returns a <see cref="CsvConverterFactory{T}"/></remarks>
    public CsvConverter<T>? TryGetConverter(Type resultType)
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
                created = true;
            }
        }

        Debug.Assert(
            converter is not CsvConverterFactory<T>,
            $"TryGetConverter returned a factory: {converter?.GetType().FullName}");

        if (converter is not null && created)
        {
            CheckConverterCacheSize();

            // ensure we return the same instance that was cached
            return _converterCache.GetOrAdd(resultType, converter);
        }

        return converter;
    }

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

    private bool TryCreateDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<T>? converter)
    {
        if (typeof(T) == typeof(char))
        {
            CsvConverter<char>? result = null;
            CsvOptions<char> options = Unsafe.As<CsvOptions<char>>(this);

            if (EnumTextConverterFactory.Instance.CanConvert(type))
            {
                result = EnumTextConverterFactory.Instance.Create(type, options);
            }
            else if (DefaultConverters.Text.Value.TryGetValue(type, out var factory))
            {
                result = factory(options);
            }
            else if (SpanTextConverterFactory.Instance.CanConvert(type))
            {
                result = SpanTextConverterFactory.Instance.Create(type, options);
            }

            if (result != null)
            {
                converter = Unsafe.As<CsvConverter<T>>(result);
                return true;
            }
        }

        if (typeof(T) == typeof(byte))
        {
            CsvConverter<byte>? result = null;
            CsvOptions<byte> options = Unsafe.As<CsvOptions<byte>>(this);

            if (EnumUtf8ConverterFactory.Instance.CanConvert(type))
            {
                result = EnumUtf8ConverterFactory.Instance.Create(type, options);
            }
            else if (DefaultConverters.Utf8.Value.TryGetValue(type, out var factory))
            {
                result = factory(options);
            }
            else if (SpanUtf8ConverterFactory.Instance.CanConvert(type))
            {
                result = SpanUtf8ConverterFactory.Instance.Create(type, options);
            }

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
    /// Returns a converter for <typeparamref cref="TValue"/>, either a configured one or from the factory.
    /// </summary>
    /// <remarks>
    /// This API is meant to be used by the source generator to produce trimming/AOT safe code.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TValue> GetOrCreate<TValue>(
        [RequireStaticDelegate] Func<CsvOptions<T>, CsvConverter<T, TValue>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        CsvConverter<T, TValue> result;

        if (TryGetExistingOrCustomConverter(typeof(TValue), out CsvConverter<T>? converter, out bool created))
        {
            result = (CsvConverter<T, TValue>)converter;

            if (created)
            {
                CheckConverterCacheSize();
                _converterCache.TryAdd(typeof(TValue), result);
            }

            return result;
        }

        result = factory(this);
        if (result is null) InvalidConverter.Throw(factory, typeof(TValue));
        return result;
    }

    /// <summary>
    /// Returns a nullable converter, falling back to the built-in one if <see cref="UseDefaultConverters"/> is true.
    /// </summary>
    /// <remarks>
    /// This API is meant to be used by the source generator to produce trimming/AOT safe code.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TValue?> GetOrCreateNullable<TValue>(
        [RequireStaticDelegate] Func<CsvOptions<T>, CsvConverter<T, TValue>> factory) where TValue : struct
    {
        if (TryGetExistingOrCustomConverter(typeof(TValue?), out CsvConverter<T>? converter, out bool created))
        {
            if (created)
            {
                CheckConverterCacheSize();
                _converterCache.TryAdd(typeof(TValue?), converter);
            }

            return (CsvConverter<T, TValue?>)converter;
        }

        if (!UseDefaultConverters) CsvConverterMissingException.Throw(typeof(TValue?));

        CsvConverter<T, TValue> inner = GetOrCreate(factory);
        if (inner is null) InvalidConverter.Throw(factory, typeof(TValue));
        return NullableConverterFactory<T>.Create(inner, GetNullToken(typeof(TValue)));
    }

    /// <summary>
    /// Returns an enum converter, falling back to the built-in one if <see cref="UseDefaultConverters"/> is true.
    /// </summary>
    /// <remarks>
    /// This API is meant to be used by the source generator to produce trimming/AOT safe code.
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
                    });
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
                    });
        }

        return GetConverter<TEnum>();
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
