using FlameCsv.Converters;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

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
    public IList<CsvConverter<T>> Converters => _converters ??= new SealableList<CsvConverter<T>>(this, defaultValues: null);

    private SealableList<CsvConverter<T>>? _converters;

    internal readonly ConcurrentDictionary<Type, CsvConverter<T>> _converterCache = new(ReferenceEqualityComparer.Instance);
    internal readonly ConcurrentDictionary<object, CsvConverter<T>> _explicitCache = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Maximum number of converters cached internally by the options instance before the cache is cleared.
    /// For converters by type, for example, with <see cref="GetConverter(Type)"/>.
    /// Default is -1
    /// </summary>
    protected int MaxConverterCacheSize
    {
        get => _maxConverterCacheSize;
        set
        {
            if (value != -1)
                ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);

            _maxConverterCacheSize = value;
        }
    }

    /// <summary>
    /// Maximum number of member converters cached internally by the options instance before the cache is cleared.
    /// For converters overridden for member, for example,
    /// with <see cref="Binding.Attributes.CsvConverterAttribute{T}"/>.
    /// Default is 256.
    /// </summary>
    protected int MaxExplicitCacheSize
    {
        get => _maxExplicitCacheSize;
        set
        {
            if (value != -1)
                ArgumentOutOfRangeException.ThrowIfLessThan(value, 0);

            _maxExplicitCacheSize = value;
        }
    }

    private int _maxConverterCacheSize = -1;
    private int _maxExplicitCacheSize = 256;

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

        return (CsvConverter<T, TResult>)GetConverter(typeof(TResult));
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

        return TryGetConverter(resultType) ?? throw new CsvConverterMissingException(resultType);
    }

    /// <summary>
    /// Returns a converter for <typeparamref name="TResult"/>, or null if not found.
    /// </summary>
    /// <remarks>Never returns a <see cref="CsvConverterFactory{T}"/></remarks>
    public CsvConverter<T, TResult>? TryGetConverter<TResult>()
    {
        return TryGetConverter(typeof(TResult)) as CsvConverter<T, TResult>;
    }

    /// <summary>
    /// Returns a converter for <paramref name="resultType"/>, or null if there is none registered
    /// </summary>
    /// <remarks>Never returns a <see cref="CsvConverterFactory{T}"/></remarks>
    public CsvConverter<T>? TryGetConverter(Type resultType)
    {
        if (!TryGetExistingOrCustomConverter(resultType, out CsvConverter<T>? converter, out bool created)
            && UseDefaultConverters)
        {
            if (TryCreateDefaultConverter(resultType, out var builtin))
            {
                Debug.Assert(builtin.CanConvert(resultType), $"Invalid builtin converter {builtin} for {resultType}");
                Debug.Assert(builtin is not CsvConverterFactory<T>, $"{resultType} default converter returned a factory");
                // TODO: set created accordingly!!
                converter = builtin;
            }
            else if (NullableConverterFactory<T>.Instance.CanConvert(resultType))
            {
                converter = NullableConverterFactory<T>.Instance.Create(resultType, this);
            }
        }

        Debug.Assert(
            converter is not CsvConverterFactory<T>,
            $"TryGetConverter returned a factory: {converter?.GetType().FullName}");

        if (converter is not null && created)
        {
            CheckConverterCacheSize();
            if (!_converterCache.TryAdd(resultType, converter))
            {
                // ensure we return the same instance that was cached
                converter = _converterCache[resultType];
            }
        }

        return converter;
    }

    private bool TryCreateDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<T>? converter)
    {
        if (typeof(T) == typeof(char))
        {
            CsvConverter<char>? result = null;
            CsvOptions<char> options = (CsvOptions<char>)(object)this;

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
            CsvOptions<byte> options = (CsvOptions<byte>)(object)this;

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

        converter = default;
        return false;
    }

    /// <inheritdoc cref="GetOrCreate{TValue}(object, CsvConverterFactory{T})"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TValue> GetOrCreate<TValue>(
        [RequireStaticDelegate] Func<CsvOptions<T>, CsvConverter<T, TValue>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        CsvConverter<T, TValue> result;

        if (TryGetExistingOrCustomConverter(typeof(TValue), out CsvConverter<T>? converter, out bool created))
        {
            result = (CsvConverter<T, TValue>)converter;
        }
        else
        {
            result = factory(this);

            if (result is null)
                InvalidConverter.Throw(factory, typeof(TValue));

            created = true;
        }

        if (created)
        {
            CheckConverterCacheSize();
            _converterCache.TryAdd(typeof(TValue), result);
        }

        return result;
    }

    /// <summary>
    /// Returns a converter for <typeparamref name="TValue"/>, or creates one using <paramref name="factory"/>.
    /// </summary>
    /// <remarks>
    /// This API is meant to be used by the source generator instead of user code.
    /// </remarks>
    /// <param name="cacheKey">Key unique for the member this converter is created for</param>
    /// <param name="factory">Factory to create a converter if none is cached</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TValue> GetOrCreate<TValue>(object cacheKey, CsvConverterFactory<T> factory)
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(factory);

        Debug.Assert(factory.CanConvert(typeof(TValue)));

        if (!_explicitCache.TryGetValue(cacheKey, out CsvConverter<T>? value))
        {
            CheckExplicitCacheSize();
            value = _explicitCache.AddOrUpdate(
                cacheKey,
                static (_, arg) =>
                {
                    var converter = arg.factory.Create(typeof(TValue), arg.options);

                    if (converter is not CsvConverter<T, TValue> || !converter.CanConvert(typeof(TValue)))
                        InvalidConverter.Throw(arg.factory, typeof(TValue));

                    return converter;
                },
                static (_, value, _) => value,
                (factory, options: this));
        }

        return (CsvConverter<T, TValue>)value;
    }

    /// <inheritdoc cref="GetOrCreate{TValue}(object, CsvConverterFactory{T})"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TValue> GetOrCreate<TValue>(
        object cacheKey,
        [RequireStaticDelegate] Func<CsvOptions<T>, CsvConverter<T, TValue>> factory)
    {
        ArgumentNullException.ThrowIfNull(cacheKey);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_explicitCache.TryGetValue(cacheKey, out CsvConverter<T>? value))
        {
            CheckExplicitCacheSize();
            value = _explicitCache.AddOrUpdate(
                cacheKey,
                static (_, arg) =>
                {
                    var result = arg.factory(arg.options);

                    if (result is null)
                        InvalidConverter.Throw(arg.factory, typeof(TValue));

                    return result;
                },
                static (_, value, _) => value,
                (factory, options: this));
        }

        return (CsvConverter<T, TValue>)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckConverterCacheSize()
    {
        if (MaxConverterCacheSize > 0 && _explicitCache.Count >= MaxConverterCacheSize)
            _converterCache.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckExplicitCacheSize()
    {
        if (MaxExplicitCacheSize > 0 && _explicitCache.Count >= MaxExplicitCacheSize)
            _explicitCache.Clear();
    }
}

file static class InvalidConverter
{
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    public static void Throw(object factory, Type toConvert)
    {
        throw new CsvConfigurationException($"{factory.GetType().FullName} returned an invalid converter for {toConvert.FullName}");
    }
}

