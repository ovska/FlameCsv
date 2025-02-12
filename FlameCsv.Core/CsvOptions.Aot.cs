using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Converters;
using FlameCsv.Exceptions;
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
        private readonly CsvOptions<T> _options;

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
            if (TryGetExistingOrCustomConverter<TResult>(out CsvConverter<T, TResult>? converter))
            {
                return converter;
            }

            if (typeof(T) == typeof(char) &&
                DefaultConverters.Text.Value.TryGetValue(typeof(TResult), out var factory1))
            {
                converter = Unsafe.As<CsvConverter<T, TResult>>(factory1(Unsafe.As<CsvOptions<char>>(this._options)));
            }

            if (typeof(T) == typeof(byte) &&
                DefaultConverters.Utf8.Value.TryGetValue(typeof(TResult), out var factory))
            {
                converter = Unsafe.As<CsvConverter<T, TResult>>(factory(Unsafe.As<CsvOptions<byte>>(this._options)));
            }

            if (converter is null)
            {
                CsvConverterMissingException.Throw(typeof(TResult));
            }

            if (_options._converterCache.TryAdd(typeof(TResult), converter))
            {
                _options.CheckConverterCacheSize();
            }

            return converter;
        }

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

            converter = factory(_options);

            if (converter is null) InvalidConverter.Throw(factory, typeof(TValue));

            if (canCache && _options._converterCache.TryAdd(typeof(TValue), converter))
            {
                _options.CheckConverterCacheSize();
            }

            return converter;
        }

        /// <summary>
        /// Returns a nullable converter,
        /// falling back to the built-in one if <see cref="UseDefaultConverters"/> is true.
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

            if (!_options.UseDefaultConverters) CsvConverterMissingException.Throw(typeof(TValue?));

            CsvConverter<T, TValue> inner = GetOrCreate(factory);
            if (inner is null) InvalidConverter.Throw(factory, typeof(TValue));

            var result = NullableConverterFactory<T>.Create(inner, _options.GetNullToken(typeof(TValue)));

            if (canCache)
            {
                _options.CheckConverterCacheSize();
                _options._converterCache.TryAdd(typeof(TValue?), result);
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
                CsvOptions<char>.AotSafeConverters @this = new(Unsafe.As<CsvOptions<char>>(_options));
                return (CsvConverter<T, TEnum>)(object)@this
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
                CsvOptions<byte>.AotSafeConverters @this = new(Unsafe.As<CsvOptions<byte>>(_options));
                return (CsvConverter<T, TEnum>)(object)@this
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

        private bool TryGetExistingOrCustomConverter<TValue>(
            [NotNullWhen(true)] out CsvConverter<T, TValue>? converter)
        {
            _options.MakeReadOnly();

            if (_options._converterCache.TryGetValue(typeof(TValue), out var cached))
            {
                converter = (CsvConverter<T, TValue>)cached;
                return true;
            }

            var local = _options._converters;

            // null if the Converters-property is never accessed (no custom converters)
            if (local is not null)
            {
                ReadOnlySpan<CsvConverter<T>> converters = local.Span;

                // Read converters in reverse order so parser added last has the highest priority
                for (int i = converters.Length - 1; i >= 0; i--)
                {
                    if (converters[i] is CsvConverter<T, TValue> converterOfT)
                    {
                        if (_options._converterCache.TryAdd(typeof(TValue), converter = converterOfT))
                        {
                            _options.CheckConverterCacheSize();
                        }

                        return true;
                    }
                }
            }

            converter = null;
            return false;
        }
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
