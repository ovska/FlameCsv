using System.ComponentModel;
using System.Diagnostics;
using FlameCsv.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Helpers;
using FlameCsv.Attributes;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using FlameCsv.Utilities;
using FlameCsv.Writing;
using JetBrains.Annotations;

namespace FlameCsv.Binding;

/// <summary>
/// Provides compile-time mapping to parse <typeparamref name="TValue"/> records from CSV.
/// </summary>
/// <remarks>
/// Decorate a non-generic <see langword="partial"/> <see langword="class"/> with <see cref="CsvTypeMapAttribute{T,TValue}"/>
/// to generate the implementation.
/// </remarks>
/// <typeparam name="T">Token type</typeparam>
/// <typeparam name="TValue">Record type</typeparam>
[PublicAPI]
public abstract class CsvTypeMap<T, TValue> : CsvTypeMap where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc/>
    protected sealed override Type TargetType => typeof(TValue);

    /// <summary>
    /// Disables caching of de/materializers for this type map.
    /// </summary>
    /// <remarks>
    /// Caching is enabled by default.
    /// The cache is used on calls to public methods on the base class <see cref="CsvTypeMap"/>.
    /// </remarks>
    /// <seealso cref="GetMaterializer(ReadOnlySpan{string},CsvOptions{T})"/>
    /// <seealso cref="GetMaterializer(FlameCsv.CsvOptions{T})"/>
    /// <seealso cref="GetDematerializer"/>
    protected virtual bool NoCaching => false;

    /// <inheritdoc cref="GetMaterializer(ReadOnlySpan{string},CsvOptions{T})"/>
    protected virtual IMaterializer<T, TValue> BindForReading(
        scoped ReadOnlySpan<string> headers,
        CsvOptions<T> options)
    {
        throw new CsvBindingException(
            $"CsvTypeMap<{typeof(T)},{typeof(TValue)}>.{nameof(BindForReading)}(ReadOnlySpan<string>, CsvOptions<{typeof(T)}>) is not overridden by {GetType().FullName}.");
    }

    /// <inheritdoc cref="GetMaterializer(CsvOptions{T})"/>
    protected virtual IMaterializer<T, TValue> BindForReading(CsvOptions<T> options)
    {
        throw new CsvBindingException(
            $"CsvTypeMap<{typeof(T)},{typeof(TValue)}>.{nameof(BindForReading)}() is not overridden by {GetType().FullName}.");
    }

    /// <inheritdoc cref="GetDematerializer"/>
    protected virtual IDematerializer<T, TValue> BindForWriting(CsvOptions<T> options)
    {
        throw new CsvBindingException(
            $"CsvTypeMap<{typeof(T)},{typeof(TValue)}>.{nameof(BindForWriting)}() is not overridden by {GetType().FullName}.");
    }

    /// <summary>
    /// Returns a materializer for <typeparamref name="TValue"/> bound to CSV header.
    /// </summary>
    /// <remarks>
    /// Caches the materializer based on the type, headers, and options. This can be overridden per type map.
    /// </remarks>
    public IMaterializer<T, TValue> GetMaterializer(scoped ReadOnlySpan<string> headers, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfZero(headers.Length);
        Throw.IfInvalidArgument(!options.HasHeader, "Options is configured to read without a header.");

        if (NoCaching || !CacheKey.CanCache(headers.Length))
            return BindForReading(headers, options);

        var key = new CacheKey(options, this, TargetType, headers);

        if (_readHeaderCache.TryGetValue(key, out object? cached))
            return (IMaterializer<T, TValue>)cached;

        var materializer = BindForReading(headers, options);
        _readHeaderCache.Add(key, materializer);
        return materializer;
    }

    /// <summary>
    /// Returns a materializer for <typeparamref name="TValue"/> bound to column indexes.
    /// </summary>
    /// <remarks>
    /// Caches the materializer based on the type and options. This can be overridden per type map.
    /// </remarks>
    public IMaterializer<T, TValue> GetMaterializer(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Throw.IfInvalidArgument(options.HasHeader, "Options is not configured to read without a header.");

        if (NoCaching)
            return BindForReading(options);

        if (_readNoHeaderCache.TryGetValue(this, out object? cached))
        {
            return (IMaterializer<T, TValue>)cached;
        }

        var materializer = BindForReading(options);
        _readNoHeaderCache.Add(this, materializer);
        return materializer;
    }

    /// <summary>
    /// Returns a dematerializer for <typeparamref name="TValue"/>.
    /// </summary>
    /// <remarks>
    /// Caches the dematerializer based on the type and options. This can be overridden per type map.
    /// </remarks>
    /// <exception cref="CsvBindingException">
    /// Options is configured not to write a header, but <typeparamref name="TValue"/> has no index binding.
    /// </exception>
    public IDematerializer<T, TValue> GetDematerializer(CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (NoCaching)
            return BindForWriting(options);

        if (_writeCache.TryGetValue(this, out object? cached))
            return (IDematerializer<T, TValue>)cached;

        var dematerializer = BindForWriting(options);
        _writeCache.Add(this, dematerializer);
        return dematerializer;
    }
}

/// <summary>
/// Base class providing throw helpers for <see cref="CsvTypeMap{T,TValue}"/>.
/// </summary>
[PublicAPI]
public abstract class CsvTypeMap
{
    private protected static readonly TrimmingCache<object, object> _readNoHeaderCache = new();
    private protected static readonly TrimmingCache<object, object> _writeCache = new();
    private protected static readonly TrimmingCache<CacheKey, object> _readHeaderCache = new();

    /// <summary>
    /// Returns the mapped type.
    /// </summary>
    protected abstract Type TargetType { get; }

    /// <summary>
    /// Throws an exception for header field being bound multiple times.
    /// </summary>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}.ThrowOnDuplicate"/>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowDuplicate(
        string member,
        string field,
        ReadOnlySpan<string> headers)
    {
        throw new CsvBindingException(
            TargetType,
            $"\"{member}\" matched to multiple headers, including '{field}' in {JoinValues(headers)}.");
    }

    /// <summary>
    /// Throws an exception for header field that wasn't matched to any member or parameter.
    /// </summary>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}.IgnoreUnmatched"/>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowUnmatched(string field, int index)
    {
        throw new CsvBindingException(TargetType, $"Unmatched header field '{field}' at index {index}.");
    }

    /// <summary>
    /// Throws an exception for a required member or parameter that wasn't bound to any of the headers.
    /// </summary>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowRequiredNotRead(IEnumerable<string> members, ReadOnlySpan<string> headers)
    {
        string missingMembers = string.Join(", ", members.Select(x => $"\"{x}\""));
        throw new CsvBindingException(
            TargetType,
            $"Required members/parameters [{missingMembers}] were not matched to any header field: [{JoinValues(headers)}]");
    }

    /// <summary>
    /// Throws an exception for header that couldn't be bound to any member of parameter.
    /// </summary>
    /// <seealso cref="CsvTypeMapAttribute{T,TValue}.IgnoreUnmatched"/>
    /// <exception cref="CsvBindingException"></exception>
    [DoesNotReturn, MethodImpl(MethodImplOptions.NoInlining)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected void ThrowNoFieldsBound(ReadOnlySpan<string> headers)
    {
        throw new CsvBindingException(
            TargetType,
            $"No header fields were matched to a member or parameter: {JoinValues(headers)}");
    }

    private static string JoinValues(ReadOnlySpan<string> values)
    {
        // should never happen
        if (values.IsEmpty)
            return "";

        var sb = new ValueStringBuilder(stackalloc char[128]);

        sb.Append('[');

        foreach (var value in values)
        {
            sb.Append('"');
            sb.Append(value);
            sb.Append("\", ");
        }

        sb.Length -= 2;
        sb.Append(']');

        return sb.ToString();
    }

    private protected sealed class CacheKey : IEquatable<CacheKey>
    {
        public static bool CanCache(int headersLength) => headersLength <= StringScratch.MaxLength;

        private readonly WeakReference<object> _options;
        private readonly WeakReference<object> _typeMap;
        private readonly Type _targetType;
        private readonly int _length;
        private StringScratch _headers;

        public CacheKey(object options, object typeMap, Type targetType, ReadOnlySpan<string> headers)
        {
            Debug.Assert(headers.Length <= StringScratch.MaxLength);

            _options = new(options);
            _typeMap = new(typeMap);
            _targetType = targetType;
            _length = headers.Length;
            _headers = default;
            headers.CopyTo(_headers!);
        }

        public bool Equals(CacheKey? other)
        {
            return
                other is not null &&
                _length == other._length &&
                _targetType == other._targetType &&
                _headers.AsSpan(_length).SequenceEqual(other._headers.AsSpan(other._length)) &&
                _options.TryGetTarget(out object? target) &&
                other._options.TryGetTarget(out object? otherTarget) &&
                ReferenceEquals(target, otherTarget) &&
                _typeMap.TryGetTarget(out object? typeMap) &&
                other._typeMap.TryGetTarget(out object? otherTypeMap) &&
                ReferenceEquals(typeMap, otherTypeMap);
        }

        public override bool Equals(object? obj) => Equals(obj as CacheKey);

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode()
            => HashCode.Combine(
                _targetType.GetHashCode(),
                _options.TryGetTarget(out object? target) ? (target?.GetHashCode() ?? 0) : 0,
                _typeMap.TryGetTarget(out object? typeMap) ? (typeMap?.GetHashCode() ?? 0) : 0,
                _length,
                HashCode<string>.Combine(_headers.AsSpan(_length)));
    }
}
