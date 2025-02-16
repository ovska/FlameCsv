using System.ComponentModel;
using System.Diagnostics;
using FlameCsv.Exceptions;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using CommunityToolkit.HighPerformance.Helpers;
using FlameCsv.Attributes;
using FlameCsv.Extensions;
using FlameCsv.Utilities;
using JetBrains.Annotations;

namespace FlameCsv.Binding;

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
    /// If <see langword="true"/>, headers that cannot be matched to a member are ignored instead of throwing.
    /// </summary>
    public bool IgnoreUnmatched { get; init; }

    /// <summary>
    /// If <see langword="true"/>, multiple header field matches to a single member throw an exception.
    /// The default behavior does not attempt to match already matched members.
    /// </summary>
    public bool ThrowOnDuplicate { get; init; }

    /// <summary>
    /// Disables caching of de/materializers for this type map.
    /// </summary>
    /// <remarks>
    /// Caching is enabled by default.
    /// The cache is used on calls to public methods on the base class <see cref="CsvTypeMap"/>.
    /// </remarks>
    /// <seealso cref="CsvTypeMap{T,TValue}.GetMaterializer(ReadOnlySpan{string},CsvOptions{T})"/>
    /// <seealso cref="CsvTypeMap{T,TValue}.GetMaterializer(FlameCsv.CsvOptions{T})"/>
    /// <seealso cref="CsvTypeMap{T,TValue}.GetDematerializer"/>
    public bool NoCaching { get; init; }

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
            $"\"{member}\" matched to multiple headers, including '{field}' in {UtilityExtensions.JoinValues(headers)}.");
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
            $"Required members/parameters [{missingMembers}] were not matched to any header field: [{UtilityExtensions.JoinValues(headers)}]");
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
            $"No header fields were matched to a member or parameter: {UtilityExtensions.JoinValues(headers)}");
    }

    private protected sealed class CacheKey : IEquatable<CacheKey>
    {
        public static bool CanCache(int headersLength) => headersLength <= StringScratch.MaxLength;

        private readonly WeakReference<object> _options;
        private readonly WeakReference<CsvTypeMap> _typeMap;
        private readonly Type _targetType;
        private readonly int _length;
        private StringScratch _headers;

        public CacheKey(object options, CsvTypeMap typeMap, Type targetType, ReadOnlySpan<string> headers)
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
                _typeMap.TryGetTarget(out CsvTypeMap? typeMap) &&
                other._typeMap.TryGetTarget(out CsvTypeMap? otherTypeMap) &&
                ReferenceEquals(typeMap, otherTypeMap);
        }

        public override bool Equals(object? obj) => Equals(obj as CacheKey);

        // ReSharper disable once NonReadonlyMemberInGetHashCode
        public override int GetHashCode()
            => HashCode.Combine(
                _targetType.GetHashCode(),
                _options.TryGetTarget(out object? target) ? (target?.GetHashCode() ?? 0) : 0,
                _typeMap.TryGetTarget(out CsvTypeMap? typeMap) ? (typeMap?.GetHashCode() ?? 0) : 0,
                _length,
                HashCode<string>.Combine(_headers.AsSpan(_length)));
    }
}
