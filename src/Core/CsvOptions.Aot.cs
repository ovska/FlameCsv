using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Converters;
using FlameCsv.Converters.Enums;
using FlameCsv.Exceptions;
using JetBrains.Annotations;

namespace FlameCsv;

partial class CsvOptions<T>
{
    /// <summary>
    /// Returns a convenience type that provides AOT-safe access to converters.
    /// </summary>
    /// <remarks>
    /// This API is meant to be used by the source generator to produce trimming/AOT safe code.
    /// Because of this, <see cref="CsvConverterFactory{T}"/> is not supported.
    /// All non-default converters must be configured manually using the <see cref="Converters"/> property.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public AotSafeConverters Aot => new(this);

    /// <summary>
    /// Wrapper around <see cref="CsvOptions{T}.Converters"/> to provide AOT-safe access to converters.
    /// </summary>
    [PublicAPI]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AotSafeConverters
    {
        internal readonly CsvOptions<T> _options;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AotSafeConverters(CsvOptions<T> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _options = options;
        }

        /// <summary>
        /// Returns a converter for <typeparamref name="TResult"/>.
        /// </summary>
        /// <typeparam name="TResult">Type to convert</typeparam>
        /// <exception cref="CsvConverterMissingException"/>
        /// <remarks>
        /// This API is meant to be used by the source generator to produce trimming/AOT safe code.<br/>
        /// If a non-factory user defined converter is found, it is returned directly.<br/>
        /// <see cref="CsvConverterFactory{T}"/> added to <see cref="Converters"/> are not supported.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public CsvConverter<T, TResult> GetConverter<TResult>()
        {
            if (Extensions.GetCachedOrCustom(_options, Guid.Empty, out CsvConverter<T, TResult>? converter))
            {
                return converter;
            }

            if (typeof(T) == typeof(char) && DefaultConverters.GetText(typeof(TResult)) is { } utf16func)
            {
                converter = (CsvConverter<T, TResult>)(object)(utf16func(Unsafe.As<CsvOptions<char>>(_options)));
            }

            if (typeof(T) == typeof(byte) && DefaultConverters.GetUtf8(typeof(TResult)) is { } utf8func)
            {
                converter = (CsvConverter<T, TResult>)(object)(utf8func(Unsafe.As<CsvOptions<byte>>(_options)));
            }

            if (converter is null)
            {
                CsvConverterMissingException.Throw(typeof(TResult));
            }

            _options.ConverterCache.TryAdd((typeof(TResult), Guid.Empty), converter);
            return converter;
        }

        /// <summary>
        /// Returns a converter for <typeparamref name="TValue"/>, either a configured one or from the factory.
        /// </summary>
        /// <param name="factory">Factory to create the converter</param>
        /// <remarks>
        /// This API is meant to be used by the source generator to produce trimming/AOT safe code.<br/>
        /// If a non-factory user defined converter is found, it is returned directly.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public CsvConverter<T, TValue> GetOrCreate<TValue>(
            [RequireStaticDelegate] Func<CsvOptions<T>, CsvConverter<T, TValue>> factory
        )
        {
            ArgumentNullException.ThrowIfNull(factory);

            if (Extensions.GetCachedOrCustom(_options, Guid.Empty, out CsvConverter<T, TValue>? converter))
            {
                return converter;
            }

            converter = factory(_options);

            if (converter is null)
            {
                throw new CsvConfigurationException(
                    $"The factory delegate passed to GetOrCreate for {typeof(TValue).FullName} returned null."
                );
            }

            _options.ConverterCache.TryAdd((typeof(TValue), Guid.Empty), converter);
            return converter;
        }

        /// <summary>
        /// Returns a converter for <typeparamref name="TValue"/>, either a configured one or from the factory.
        /// </summary>
        /// <param name="factory">Factory to create the converter</param>
        /// <param name="identifier">
        /// Unique identifier for the target member or constructor parameter.
        /// </param>
        /// <remarks>
        /// This API is meant to be used by the source generator to produce trimming/AOT safe code.<br/>
        /// If a non-factory user defined converter is found, it is returned directly.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public TConverter GetOrCreateOverridden<TValue, TConverter>(
            [RequireStaticDelegate] Func<CsvOptions<T>, TConverter> factory,
            Guid identifier
        )
            where TConverter : CsvConverter<T, TValue>
        {
            if (identifier == Guid.Empty)
            {
                throw new ArgumentException("Overridden converter's identifier must not be empty.", nameof(identifier));
            }

            if (_options.ConverterCache.TryGetValue((typeof(TValue), identifier), out CsvConverter<T>? cached))
            {
                return (TConverter)cached;
            }

            TConverter converter =
                factory(_options)
                ?? throw new CsvConfigurationException(
                    $"The factory delegate passed to GetOrCreateOverridden for {typeof(TValue).FullName} returned null."
                );

            _options.ConverterCache.TryAdd((typeof(TValue), identifier), converter);
            return converter;
        }

        /// <summary>
        /// Returns a nullable converter,
        /// falling back to the built-in one if <see cref="UseDefaultConverters"/> is true.
        /// </summary>
        /// <param name="factory">Factory to create the inner converter</param>
        /// <param name="identifier">
        /// Unique identifier for the target member or constructor parameter.
        /// If none is specified, the converter is global and the value should be <c>Guid.Empty</c>.
        /// </param>
        /// <remarks>
        /// This API is meant to be used by the source generator to produce trimming/AOT safe code.<br/>
        /// If a non-factory user defined converter is found, it is returned directly.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public CsvConverter<T, TValue?> GetOrCreateNullable<TValue>(
            [RequireStaticDelegate] Func<CsvOptions<T>, CsvConverter<T, TValue>> factory,
            Guid identifier = default
        )
            where TValue : struct
        {
            ArgumentNullException.ThrowIfNull(factory);

            if (Extensions.GetCachedOrCustom(_options, identifier, out CsvConverter<T, TValue?>? converter))
            {
                return converter;
            }

            if (!_options.UseDefaultConverters)
            {
                CsvConverterMissingException.Throw(typeof(TValue?));
            }

            CsvConverter<T, TValue> inner =
                factory(_options)
                ?? throw new CsvConfigurationException(
                    $"The factory delegate passed to GetOrCreateNullable for {typeof(TValue).FullName} returned null."
                );

            var result = new NullableConverter<T, TValue>(inner, _options.GetNullToken(typeof(TValue)));
            _options.ConverterCache.TryAdd((typeof(TValue?), identifier), result);
            return result;
        }

        /// <summary>
        /// Creates a converter for an enum type.
        /// </summary>
        /// <remarks>
        /// This API is meant to be used by the source generator to produce trimming/AOT safe code.<br/>
        /// If a non-factory user defined converter is found, it is returned directly.
        /// </remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public CsvConverter<T, TEnum> GetOrCreateEnum<TEnum>()
            where TEnum : struct, Enum
        {
            if (typeof(T) == typeof(char))
            {
                CsvOptions<char> options = new(Unsafe.As<CsvOptions<char>>(_options));

                CsvConverter<char, TEnum> converter = Extensions.GetOrCreate(
                    options,
                    Guid.Empty,
                    static o =>
                    {
                        if (!o.UseDefaultConverters)
                            CsvConverterMissingException.Throw(typeof(TEnum));
                        return new EnumTextConverter<TEnum>(o);
                    }
                );

                return (CsvConverter<T, TEnum>)(object)converter;
            }

            if (typeof(T) == typeof(byte))
            {
                CsvOptions<byte> options = new(Unsafe.As<CsvOptions<byte>>(_options));

                CsvConverter<byte, TEnum> converter = Extensions.GetOrCreate(
                    options,
                    Guid.Empty,
                    static o =>
                    {
                        if (!o.UseDefaultConverters)
                            CsvConverterMissingException.Throw(typeof(TEnum));
                        return new EnumUtf8Converter<TEnum>(o);
                    }
                );

                return (CsvConverter<T, TEnum>)(object)converter;
            }

            throw Token<T>.NotSupported;
        }
    }
}

file static class Extensions
{
    public static CsvConverter<T, TValue> GetOrCreate<T, TValue>(
        CsvOptions<T> options,
        Guid identifier,
        [RequireStaticDelegate] Func<CsvOptions<T>, CsvConverter<T, TValue>> factory
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (GetCachedOrCustom(options, identifier, out CsvConverter<T, TValue>? converter))
        {
            return converter;
        }

        converter =
            factory(options)
            ?? throw new CsvConfigurationException(
                $"The factory delegate passed to GetOrCreate for {typeof(TValue).FullName} returned null."
            );

        options.ConverterCache.TryAdd((typeof(TValue), identifier), converter);
        return converter;
    }

    public static bool GetCachedOrCustom<T, TValue>(
        CsvOptions<T> options,
        Guid identifier,
        [NotNullWhen(true)] out CsvConverter<T, TValue>? converter
    )
        where T : unmanaged, IBinaryInteger<T>
    {
        options.MakeReadOnly();

        if (options.ConverterCache.TryGetValue((typeof(TValue), identifier), out var cached))
        {
            converter = (CsvConverter<T, TValue>)cached;
            return true;
        }

        var local = options._converters;

        // null if the Converters-property is never accessed (no custom converters)
        if (local is not null)
        {
            ReadOnlySpan<ConverterBuilder<T>> converters = local.Span;

            // Read converters in reverse order so parser added last has the highest priority
            for (int i = converters.Length - 1; i >= 0; i--)
            {
                if (
                    converters[i].CanConvert(typeof(TValue))
                    && converters[i].Unwrap() is CsvConverter<T, TValue> converterOfT
                )
                {
                    options.ConverterCache.TryAdd((typeof(TValue), identifier), converter = converterOfT);
                    return true;
                }
            }
        }

        converter = null;
        return false;
    }
}
