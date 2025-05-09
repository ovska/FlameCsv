using System.Collections.Immutable;
using FlameCsv.Attributes;
using FlameCsv.Exceptions;
using FlameCsv.Extensions;
using FlameCsv.Reading;
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
public abstract class CsvTypeMap<T, TValue> : CsvTypeMap
    where T : unmanaged, IBinaryInteger<T>
{
    /// <inheritdoc/>
    protected sealed override Type TargetType => typeof(TValue);

    /// <inheritdoc cref="GetMaterializer(ImmutableArray{string},CsvOptions{T})"/>
    protected virtual IMaterializer<T, TValue> BindForReading(ImmutableArray<string> headers, CsvOptions<T> options)
    {
        throw new CsvBindingException(
            $"CsvTypeMap<{typeof(T)},{typeof(TValue)}>.{nameof(BindForReading)}(ReadOnlySpan<string>, CsvOptions<{typeof(T)}>) is not overridden by {GetType().FullName}."
        );
    }

    /// <inheritdoc cref="GetMaterializer(CsvOptions{T})"/>
    protected virtual IMaterializer<T, TValue> BindForReading(CsvOptions<T> options)
    {
        throw new CsvBindingException(
            $"CsvTypeMap<{typeof(T)},{typeof(TValue)}>.{nameof(BindForReading)}() is not overridden by {GetType().FullName}."
        );
    }

    /// <inheritdoc cref="GetDematerializer"/>
    protected virtual IDematerializer<T, TValue> BindForWriting(CsvOptions<T> options)
    {
        throw new CsvBindingException(
            $"CsvTypeMap<{typeof(T)},{typeof(TValue)}>.{nameof(BindForWriting)}() is not overridden by {GetType().FullName}."
        );
    }

    /// <summary>
    /// Returns a materializer for <typeparamref name="TValue"/> bound to CSV header.
    /// </summary>
    /// <remarks>
    /// Caches the materializer based on the type, headers, and options. This can be overridden per type map.
    /// </remarks>
    public IMaterializer<T, TValue> GetMaterializer(ImmutableArray<string> headers, CsvOptions<T> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfZero(headers.Length);
        Throw.IfInvalidArgument(!options.HasHeader, "Options is configured to read without a header.");

        if (NoCaching)
            return BindForReading(headers, options);
        return options.GetMaterializer(
            this,
            headers,
            static (options, typeMap, headers) => typeMap.BindForReading(headers, options)
        );
    }

    /// <summary>
    /// Returns a materializer for <typeparamref name="TValue"/> bound to field indexes.
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
        return options.GetMaterializer(this, [], static (options, typeMap, _) => typeMap.BindForReading(options));
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

        return options.GetDematerializer(this, static (options, typeMap) => typeMap.BindForWriting(options));
    }
}
