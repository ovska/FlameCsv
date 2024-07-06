using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Binding;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Converters;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using CommunityToolkit.HighPerformance.Buffers;
using CommunityToolkit.Diagnostics;
using FlameCsv.Reading;
using System.Text;
using FlameCsv.Converters.Text;
using System.Globalization;
using static FlameCsv.Utilities.SealableUtil;
using System.ComponentModel;

namespace FlameCsv;

file static class DefaultOptionsHolder
{
    public static readonly Lazy<CsvOptions<char>> Text = new(() =>
    {
        var o = new CsvOptions<char>();
        o.MakeReadOnly();
        return o;
    });

    public static readonly Lazy<CsvOptions<byte>> Utf8 = new(() =>
    {
        var o = new CsvOptions<byte>();
        o.MakeReadOnly();
        return o;
    });
}

/// <summary>
/// Object used to configure dialect, converters and other options to read and write CSV data.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
public partial class CsvOptions<T> : ISealable where T : unmanaged, IEquatable<T>
{
    /// <summary>
    /// Returns read-only default options for <typeparamref name="T"/>, with same configuration as <see langword="new"/>().
    /// </summary>
    /// <remarks>
    /// Throws <see cref="NotSupportedException"/> for token types other than <see langword="char"/> or <see langword="byte"/>.
    /// </remarks>
    public static CsvOptions<T> Default
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (typeof(T) == typeof(char))
                return Unsafe.As<CsvOptions<T>>(DefaultOptionsHolder.Text.Value);

            if (typeof(T) == typeof(byte))
                return Unsafe.As<CsvOptions<T>>(DefaultOptionsHolder.Utf8.Value);

            ThrowInvalidTokenType(nameof(Default));
            return default!;
        }
    }

    /// <summary>
    /// Initializes an options-instance with default configuration.
    /// </summary>
    public CsvOptions()
    {
        _converters = new SealableList<CsvConverter<T>>(this, defaultValues: null);

#if DEBUG
        _allowContentInExceptions = true;
#endif
    }

    protected CsvOptions(CsvOptions<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        _shouldSkipRow = other._shouldSkipRow;
        _exceptionHandler = other._exceptionHandler;
        _hasHeader = other._hasHeader;
        _validateFieldCount = other._validateFieldCount;
        _fieldEscaping = other._fieldEscaping;
        _arrayPool = other._arrayPool;
        _allowContentInExceptions = other._allowContentInExceptions;
        _booleanValues = other._booleanValues;
        _formatProvider = other._formatProvider;
        _providers = other._providers?.Clone();
        _formats = other._formats?.Clone();
        _comparer = other._comparer;
        _useDefaultConverters = other._useDefaultConverters;
        _ignoreEnumCase = other._ignoreEnumCase;
        _enumFormat = other._enumFormat;
        _allowUndefinedEnumValues = other._allowUndefinedEnumValues;
        _disableBuffering = other._disableBuffering;
        _stringPool = other._stringPool;
        _null = other._null;
        _nullTokens = other._nullTokens?.Clone();
        _converters = new(this, other._converters); // copy collection
    }

    /// <summary>
    /// Whether the options instance is sealed and can no longer be modified.
    /// Options become read only after they begin being used to avoid concurrency bugs.
    /// </summary>
    public bool IsReadOnly { get; private set; }

    /// <summary>
    /// Seals the instance from modifications.
    /// </summary>
    /// <returns><see langword="true"/> if the instance was made readonly, <see langword="false"/> if it already was.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MakeReadOnly()
    {
        if (IsReadOnly)
            return false;

        InitializeDialect();
        IsReadOnly = true;
        return true;
    }

    /// <summary>
    /// Returns the header binder that matches CSV header record fields to parsed type's properties/fields.
    /// </summary>
    /// <remarks>
    /// By default, CSV header is matched to property/field names and
    /// <see cref="Binding.Attributes.CsvHeaderAttribute"/> using <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </remarks>
    public virtual IHeaderBinder<T> GetHeaderBinder() => new DefaultHeaderBinder<T>(this);

    /// <summary>
    /// Returns the <see langword="null"/> value for parsing and formatting for the parameter type.
    /// Returns <see cref="Null"/> if none is configured.
    /// </summary>
    public virtual ReadOnlyMemory<T> GetNullToken(Type resultType)
    {
        if (typeof(T) != typeof(char) && typeof(T) != typeof(byte))
        {
            ThrowInvalidTokenType(nameof(GetNullToken));
        }

        if (_nullTokens is not null)
        {
            // allow fetching null tokens for both int and int?
            Type? alternate = resultType.IsValueType ? Nullable.GetUnderlyingType(resultType) : null;

            if (typeof(T) == typeof(char))
            {
                if (_nullTokens.TryGetValue(resultType, out string? value) ||
                    (alternate is not null && _nullTokens.TryGetValue(alternate, out value)))
                {
                    var result = value.AsMemory();
                    return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref result);
                }
            }

            if (typeof(T) == typeof(byte))
            {
                if (_nullTokens.TryGetAlternate(resultType, out Utf8String value) ||
                    (alternate is not null && _nullTokens.TryGetAlternate(alternate, out value)))
                {
                    ReadOnlyMemory<byte> result = value;
                    return Unsafe.As<ReadOnlyMemory<byte>, ReadOnlyMemory<T>>(ref result);
                }
            }
        }

        return GetDefaultNullToken();
    }

    private ReadOnlyMemory<T> GetDefaultNullToken()
    {
        if (_null is null)
            return default;

        if (typeof(T) == typeof(char))
        {
            var value = Unsafe.As<string?>(_null).AsMemory();
            return Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref value);
        }

        if (typeof(T) == typeof(byte))
        {
            var value = (ReadOnlyMemory<byte>)Unsafe.As<Tuple<Utf8String>>(_null).Item1;
            return Unsafe.As<ReadOnlyMemory<byte>, ReadOnlyMemory<T>>(ref value);
        }

        ThrowInvalidTokenType(nameof(GetNullToken));
        return default;
    }

    /// <summary>
    /// Returns the format configured for <paramref name="resultType"/>, or <paramref name="defaultValue"/> by default.
    /// </summary>
    public virtual string? GetFormat(Type resultType, string? defaultValue = null) => _formats is not null && _formats.TryGetValue(resultType, out var format) ? format : defaultValue;

    /// <summary>
    /// Returns the format provider configured for <paramref name="resultType"/>, or <see cref="FormatProvider"/> by default.
    /// </summary>
    public virtual IFormatProvider? GetFormatProvider(Type resultType) => _providers is not null && _providers.TryGetValue(resultType, out var provider) ? provider : _formatProvider;

    /// <summary>
    /// Returns a <see langword="string"/> representation of the value.
    /// </summary>
    /// <seealso cref="WriteText{TWriter}(TWriter, ReadOnlySpan{char})"/>
    public virtual string GetAsString(ReadOnlySpan<T> field)
    {
        if (typeof(T) == typeof(char))
        {
            return field.UnsafeCast<T, char>().ToString();
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.GetString(field.UnsafeCast<T, byte>());
        }

        ThrowInvalidTokenType(nameof(GetAsString));
        return default!;
    }

    /// <summary>
    /// Writes <paramref name="destination"/> as chars to <paramref name="destination"/>.
    /// </summary>
    public virtual bool TryGetChars(ReadOnlySpan<T> field, Span<char> destination, out int charsWritten)
    {
        if (typeof(T) == typeof(char))
        {
            return field.UnsafeCast<T, char>().TryWriteTo(destination, out charsWritten);
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetChars(field.UnsafeCast<T, byte>(), destination, out charsWritten);
        }

        ThrowInvalidTokenType(nameof(TryGetChars));
        Unsafe.SkipInit(out charsWritten);
        return default;
    }

    /// <summary>
    /// Writes <paramref name="value"/> as <typeparamref name="T"/> to <paramref name="destination"/>.
    /// </summary>
    /// <param name="value">Text to write</param>
    /// <seealso cref="GetAsString(ReadOnlySpan{T})"/>
    public virtual bool TryWriteChars(ReadOnlySpan<char> value, Span<T> destination, out int charsWritten)
    {
        if (typeof(T) == typeof(char))
        {
            return value.UnsafeCast<char, T>().TryWriteTo(destination, out charsWritten);
        }

        if (typeof(T) == typeof(byte))
        {
            return Encoding.UTF8.TryGetBytes(value, destination.UnsafeCast<T, byte>(), out charsWritten);
        }

        ThrowInvalidTokenType(nameof(TryWriteChars));
        Unsafe.SkipInit(out charsWritten);
        return default;
    }

    internal CsvRecordSkipPredicate<T>? _shouldSkipRow;
    internal CsvExceptionHandler<T>? _exceptionHandler;
    internal bool _hasHeader = true;
    internal bool _validateFieldCount;
    internal CsvFieldEscaping _fieldEscaping;
    internal ArrayPool<T> _arrayPool = ArrayPool<T>.Shared;
    private bool _allowContentInExceptions;
    internal IList<(string text, bool value)>? _booleanValues;
    private IFormatProvider? _formatProvider = CultureInfo.InvariantCulture;
    private TypeDictionary<IFormatProvider?, object>? _providers;
    internal TypeDictionary<string?, object>? _formats;
    private IEqualityComparer<string> _comparer = StringComparer.OrdinalIgnoreCase;
    private bool _useDefaultConverters = true;
    private bool _ignoreEnumCase = true;
    private string? _enumFormat;
    private bool _allowUndefinedEnumValues;
    private bool _disableBuffering;
    private StringPool? _stringPool;
    private object? _null;
    private TypeDictionary<string?, Utf8String>? _nullTokens;

    public virtual string? Null
    {
        get
        {
            if (_null is null)
                return null;

            if (typeof(T) == typeof(char))
                return Unsafe.As<string?>(_null);

            if (typeof(T) == typeof(byte))
                return (string)Unsafe.As<Tuple<Utf8String>>(_null).Item1;

            ThrowInvalidTokenType(nameof(Null));
            return null;
        }
        set
        {
            this.ThrowIfReadOnly();

            if (value is null || typeof(T) == typeof(char))
            {
                _null = value;
                return;
            }

            if (typeof(T) == typeof(byte))
            {
                _null = new Tuple<Utf8String>(value);
                return;
            }

            ThrowInvalidTokenType(nameof(Null));
        }
    }

    /// <summary>
    /// Format provider used it none is defined for type in <see cref="FormatProviders"/>.
    /// Defaults to <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public IFormatProvider? FormatProvider
    {
        get => _formatProvider;
        set => this.SetValue(ref _formatProvider, value);
    }

    /// <summary>
    /// Format provider user per type instead of <see cref="FormatProvider"/>.
    /// </summary>
    public IDictionary<Type, IFormatProvider?> FormatProviders => _providers ??= new TypeDictionary<IFormatProvider?, object>(this);

    /// <summary>
    /// Format used per type.
    /// </summary>
    public IDictionary<Type, string?> Formats => _formats ??= new TypeDictionary<string?, object>(this);

    /// <summary>
    /// Disables buffering newline ranges when reading. Buffering increases raw throughput,
    /// but can in some cases raise errors later in the parsing pipeline than without.
    /// </summary>
    public bool NoLineBuffering
    {
        get => _disableBuffering;
        set => this.SetValue(ref _disableBuffering, value);
    }

    /// <summary>
    /// Text comparison used to match header names.
    /// </summary>
    /// <remarks>
    /// Header names are converted to strings using <see cref="GetAsString(ReadOnlySpan{T})"/>.
    /// </remarks>
    public IEqualityComparer<string> Comparer
    {
        get => _comparer;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            this.SetValue(ref _comparer, value);
        }
    }

    /// <summary>
    /// Delegate that determines whether a row should be skipped.
    /// Default is <see langword="null"/>, which means all rows are processed.
    /// </summary>
    public CsvRecordSkipPredicate<T>? ShouldSkipRow
    {
        get => _shouldSkipRow;
        set => this.SetValue(ref _shouldSkipRow, value);
    }

    /// <summary>
    /// Delegate that is called when an exception is thrown while parsing class records. If null (the default), or the
    /// delegate returns false, the exception is considered unhandled and is thrown.<para/>For example, to ignore
    /// unparseable values return <see langword="true"/> if the exception is <see cref="CsvParseException"/>. In
    /// this case, rows with invalid data are skipped, see also: <see cref="ShouldSkipRow"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="CsvFormatException"/> is not handled, as it represents an invalid CSV.<br/>
    /// This handler is not used in the enumerators that return <see cref="CsvValueRecord{T}"/>, you can catch
    /// exceptions thrown manually when handling the record.
    /// </remarks>
    public CsvExceptionHandler<T>? ExceptionHandler
    {
        get => _exceptionHandler;
        set => this.SetValue(ref _exceptionHandler, value);
    }

    /// <summary>
    /// String pool to use when parsing strings. Default is <see langword="null"/>, which results in no pooling.
    /// </summary>
    /// <remarks>
    /// Pooling reduces raw throughput, but can have profound impact on allocations if the data has a lot of repeating strings.
    /// </remarks>
    public StringPool? StringPool
    {
        get => _stringPool;
        set => this.SetValue(ref _stringPool, value);
    }

    /// <summary>
    /// If <see langword="true"/>, CSV content is included in exception messages. Default is
    /// <see langword="false"/>, which will only show the CSV structure relative to delimiters/quotes/newlines.
    /// </summary>
    public bool AllowContentInExceptions
    {
        get => _allowContentInExceptions;
        set => this.SetValue(ref _allowContentInExceptions, value);
    }

    /// <summary>
    /// Whether the read CSV has a header record. The default is <see langword="true"/>.
    /// </summary>
    /// <seealso cref="HeaderBinder"/>
    public bool HasHeader
    {
        get => _hasHeader;
        set => this.SetValue(ref _hasHeader, value);
    }

    /// <summary>
    /// Whether to ensure that all records have the same number of fields. The first read or written CSV recoird
    /// is used as the source of truth for the record count, regardless of whether it was a header record or not.
    /// Object parsing always validates the field count. Default is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Example: reading/writing a header with 5 fields ensures that every record read/written afterwards has exactly 5 fields.
    /// </remarks>
    public bool ValidateFieldCount
    {
        get => _validateFieldCount;
        set => this.SetValue(ref _validateFieldCount, value);
    }

    /// <summary>
    /// Defines the quoting behavior when writing values. Default is <see cref="CsvFieldEscaping.Auto"/>.
    /// </summary>
    public CsvFieldEscaping FieldEscaping
    {
        get => _fieldEscaping;
        set
        {
            if (!Enum.IsDefined(value))
                ThrowHelper.ThrowArgumentException(nameof(value));

            this.SetValue(ref _fieldEscaping, value);
        }
    }

    /// <summary>
    /// Whether to use the library's built-in converters. Default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// The following types are supported by default:<br/>
    /// - <see langword="string"/><br/>
    /// - <see langword="enum"/><br/>
    /// - <see langword="bool"/><br/>
    /// - <see langword="byte"/><br/>
    /// - <see langword="sbyte"/><br/>
    /// - <see langword="short"/><br/>
    /// - <see langword="ushort"/><br/>
    /// - <see langword="int"/><br/>
    /// - <see langword="uint"/><br/>
    /// - <see langword="long"/><br/>
    /// - <see langword="ulong"/><br/>
    /// - <see langword="nint"/><br/>
    /// - <see langword="nuint"/><br/>
    /// - <see langword="float"/><br/>
    /// - <see langword="double"/><br/>
    /// - <see langword="decimal"/><br/>
    /// - <see cref="Guid"/><br/>
    /// - <see cref="DateTime"/><br/>
    /// - <see cref="DateTimeOffset"/><br/>
    /// - <see cref="TimeSpan"/><br/>
    /// - For <see langword="char"/> any type that implements <see cref="ISpanFormattable"/> and <see cref="ISpanParsable{TSelf}"/>.<br/>
    /// - For <see langword="byte"/> any type that implements at least one of <see cref="IUtf8SpanFormattable"/> and
    /// <see cref="IUtf8SpanParsable{TSelf}"/>, with <see cref="ISpanFormattable"/> and <see cref="ISpanParsable{TSelf}"/> as fallbacks.<br/>
    /// </remarks>
    public bool UseDefaultConverters
    {
        get => _useDefaultConverters;
        set => this.SetValue(ref _useDefaultConverters, value);
    }

    /// <summary>
    /// Whether to ignore case when parsing enum values. Default is <see langword="true"/>.
    /// </summary>
    public bool IgnoreEnumCase
    {
        get => _ignoreEnumCase;
        set => this.SetValue(ref _ignoreEnumCase, value);
    }

    /// <summary>
    /// Whether to allow enum values that are not defined in the enum type.
    /// Default is <see langword="false"/>.
    /// </summary>
    public bool AllowUndefinedEnumValues
    {
        get => _allowUndefinedEnumValues;
        set => this.SetValue(ref _allowUndefinedEnumValues, value);
    }

    /// <summary>
    /// The default format for enums, used if enum's format is not defined in <see cref="Formats"/>.
    /// </summary>
    public string? EnumFormat
    {
        get => _enumFormat;
        set
        {
            // validate
            _ = Enum.TryFormat(default(CsvBindingScope), default, out _, value);
            this.SetValue(ref _enumFormat, value);
        }
    }

    /// <summary>
    /// Pool used to create reusable buffers when needed. Default is <see cref="ArrayPool{T}.Shared"/>.
    /// Set to <see langword="null"/> to disable pooling and always allocate.
    /// </summary>
    public ArrayPool<T>? ArrayPool
    {
        get => ReferenceEquals(_arrayPool, AllocatingArrayPool<T>.Instance) ? null : _arrayPool;
        set => this.SetValue(ref _arrayPool, value ?? AllocatingArrayPool<T>.Instance);
    }

    /// <summary>
    /// Returns tokens used to parse and format <see langword="null"/> values. See <see cref="GetNullToken(Type)"/>.
    /// </summary>
    /// <remarks>
    /// Adding a token for value type is equivalent to adding it to the <see cref="Nullable{T}"/> counterpart.
    /// </remarks>
    public IDictionary<Type, string?> NullTokens => _nullTokens ??= new(this, static str => (Utf8String)str);

    /// <summary>
    /// Optional custom boolean value mapping. If not empty, must contain at least one value for both
    /// <see langword="true"/> and <see langword="false"/>. Default is empty.
    /// </summary>
    public IList<(string text, bool value)> BooleanValues => _booleanValues ??= new SealableList<(string, bool)>(this, null);

    internal TValue Materialize<TValue>(ReadOnlyMemory<T> record, IMaterializer<T, TValue> materializer)
    {
        ArgumentNullException.ThrowIfNull(materializer);
        MakeReadOnly();

        T[]? array = null;

        try
        {
            var meta = CsvParser<T>.GetRecordMeta(record, this);
            CsvFieldReader<T> reader = new(
                this,
                record,
                stackalloc T[Token<T>.StackLength],
                ref array,
                in meta);

            return materializer.Parse(ref reader);
        }
        finally
        {
            _arrayPool?.EnsureReturned(ref array);
        }
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

        ReadOnlySpan<CsvConverter<T>> converters = _converters.Span;

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

        converter = null;
        created = false;
        return false;
    }
}
