using CommunityToolkit.Diagnostics;
using FlameCsv.Converters.Text;
using FlameCsv.Converters;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.ComponentModel;
using static FastExpressionCompiler.ImTools.FHashMap;
using System.Runtime.CompilerServices;

namespace FlameCsv;

partial class CsvOptions<T>
{
    /// <summary>
    /// Collection of all converters and factories of the options instance.
    /// </summary>
    /// <remarks>
    /// Modifying the collection after the options instance is used (<see cref="IsReadOnly"/> is <see langword="true"/>)
    /// results in an exception.
    /// </remarks>
    public IList<CsvConverter<T>> Converters => _converters;

    private readonly SealableList<CsvConverter<T>> _converters;
    internal readonly ConcurrentDictionary<Type, CsvConverter<T>> _converterCache = new(ReferenceEqualityComparer.Instance);
    internal readonly ConcurrentDictionary<object, CsvConverter<T>> _explicitCache = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Returns a converter for <typeparamref name="TResult"/>.
    /// </summary>
    /// <typeparam name="TResult">Type to convert</typeparam>
    /// <exception cref="CsvConverterMissingException"/>
    public CsvConverter<T, TResult> GetConverter<TResult>()
    {
        return (CsvConverter<T, TResult>)GetConverter(typeof(TResult));
    }

    /// <summary>
    /// Returns a converter for values of the parameter type.
    /// </summary>
    /// <remarks>
    /// Never returns a factory.
    /// </remarks>
    /// <param name="resultType">Type to convert</param>
    /// <exception cref="CsvConverterMissingException"/>
    public CsvConverter<T> GetConverter(Type resultType)
    {
        return TryGetConverter(resultType) ?? throw new CsvConverterMissingException(typeof(T), resultType);
    }

    public CsvConverter<T, TResult>? TryGetConverter<TResult>()
    {
        return TryGetConverter(typeof(TResult)) as CsvConverter<T, TResult>;
    }

    /// <summary>
    /// Returns a converter for values of the parameter type, or null if there is no
    /// converter registered for <paramref name="resultType"/>.
    /// </summary>
    /// <param name="resultType">Type to convert</param>
    public CsvConverter<T>? TryGetConverter(Type resultType)
    {
        if (!TryGetExistingOrCustomConverter(resultType, out CsvConverter<T>? converter, out bool created)
            && UseDefaultConverters)
        {
            if (TryCreateDefaultConverter(resultType, out var builtin))
            {
                Debug.Assert(builtin.CanConvert(resultType), $"Invalid builtin converter {builtin} for {resultType}");
                Debug.Assert(builtin is not CsvConverterFactory<T>, $"{resultType} default converter returned a factory");
                converter = builtin;
            }
            else if (NullableConverterFactory<T>.Instance.CanConvert(resultType))
            {
                converter = NullableConverterFactory<T>.Instance.Create(resultType, this);
            }
        }

        if (converter is not null && created && !_converterCache.TryAdd(resultType, converter))
        {
            // ensure we return the same instance that was cached
            converter = _converterCache[resultType];
        }

        Debug.Assert(
            converter is not CsvConverterFactory<T>,
            $"TryGetConverter returned a factory: {converter?.GetType().ToTypeString()}");

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
                converter = (CsvConverter<T>)(object)result;
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
                converter = (CsvConverter<T>)(object)result;
                return true;
            }
        }

        converter = default;
        return false;
    }

    /// <inheritdoc cref="GetOrCreate{TValue}(object, CsvConverterFactory{T})"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TValue> GetOrCreate<TValue>(Func<CsvOptions<T>, CsvConverter<T, TValue>> factory)
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
                ThrowForInvalidConverter(factory, typeof(TValue));

            created = true;
        }

        if (created)
        {
            _converterCache.TryAdd(typeof(TValue), result);
        }

        return result;
    }

    /// <summary>
    /// Returns a converter for <typeparamref name="TValue"/>, or creates one using <paramref name="factory"/>.
    /// </summary>
    /// <remarks>
    /// This API is meant to be used by the source generator, and not called directly.
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
                        ThrowForInvalidConverter(arg.factory, typeof(TValue));

                    return converter;
                },
                static (_, value, _) => value,
                (factory, options: this));
        }

        return (CsvConverter<T, TValue>)value;
    }

    /// <inheritdoc cref="GetOrCreate{TValue}(object, CsvConverterFactory{T})"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CsvConverter<T, TValue> GetOrCreate<TValue>(object cacheKey, Func<CsvOptions<T>, CsvConverter<T, TValue>> factory)
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
                        ThrowForInvalidConverter(arg.factory, typeof(TValue));

                    return result;
                },
                static (_, value, _) => value,
                (factory, options: this));
        }

        return (CsvConverter<T, TValue>)value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckExplicitCacheSize()
    {
        if (_explicitCache.Count > 256)
            _explicitCache.Clear();
    }

    [DoesNotReturn]
    private static void ThrowForInvalidConverter(object factory, Type toConvert)
    {
        throw new CsvConfigurationException($"{factory.GetType().ToTypeString()} returned an invalid converter for {toConvert.ToTypeString()}");
    }
}
