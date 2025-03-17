using FlameCsv.Converters;
using FlameCsv.Exceptions;
using FlameCsv.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Extensions;
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
    protected internal ConcurrentDictionary<Type, CsvConverter<T>> ConverterCache { get; }
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
            return ConverterCache.GetOrAdd(resultType, converter);
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

        if (ConverterCache.TryGetValue(resultType, out var cached))
        {
            Debug.Assert(cached.CanConvert(resultType));
            converter = cached;
            created = false;
            return true;
        }

        var local = _converters;

        // null if the Converters-property is never accessed (no custom converters)
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

    [RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
    private bool TryCreateDefaultConverter(Type type, [NotNullWhen(true)] out CsvConverter<T>? converter)
    {
        if (typeof(T) == typeof(char))
        {
            CsvOptions<char> options = Unsafe.As<CsvOptions<char>>(this);

            CsvConverter<char>? result = DefaultConverters.GetText(type) is { } factory
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

            CsvConverter<byte>? result = DefaultConverters.GetUtf8(type) is { } factory
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckConverterCacheSize()
    {
        if (ConverterCache.Count >= MaxConverterCacheSize) ConverterCache.Clear();
    }
}

[RDC(Messages.ConverterFactories), RUF(Messages.ConverterFactories)]
file static class DefaultConverterFactories
{
    public static CsvConverter<char>? TryCreateChar(Type type, CsvOptions<char> options)
    {
        if (EnumTextConverterFactory.Instance.CanConvert(type))
        {
            return EnumTextConverterFactory.Instance.Create(type, options);
        }

        if (SpanTextConverterFactory.Instance.CanConvert(type))
        {
            return SpanTextConverterFactory.Instance.Create(type, options);
        }

        return null;
    }

    public static CsvConverter<byte>? TryCreateByte(Type type, CsvOptions<byte> options)
    {
        if (EnumUtf8ConverterFactory.Instance.CanConvert(type))
        {
            return EnumUtf8ConverterFactory.Instance.Create(type, options);
        }

        if (SpanUtf8ConverterFactory.Instance.CanConvert(type))
        {
            return SpanUtf8ConverterFactory.Instance.Create(type, options);
        }

        return null;
    }
}
