using System.Collections.Immutable;
using FlameCsv.Exceptions;
using FlameCsv.Reading;
using FlameCsv.Writing;

namespace FlameCsv.Binding;

/// <summary>
/// Instance that binds CSV fields to members when reading and writing.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ICsvTypeBinder<T> where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// Returns a materializer for <typeparamref name="TValue"/> bound to a CSV header.
    /// </summary>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    IMaterializer<T, TValue> GetMaterializer<[DAM(Messages.ReflectionBound)] TValue>(ImmutableArray<string> headers);

    /// <summary>
    /// Returns a materializer for <typeparamref name="TValue"/> bound to field indexes.
    /// </summary>
    /// <exception cref="CsvBindingException">
    /// Options is configured not to write a header, but <typeparamref name="TValue"/> has no index binding.
    /// </exception>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    IMaterializer<T, TValue> GetMaterializer<[DAM(Messages.ReflectionBound)] TValue>();

    /// <summary>
    /// Returns a dematerializer for <typeparamref name="TValue"/>.
    /// </summary>
    /// <exception cref="CsvBindingException">
    /// Options is configured not to write a header, but <typeparamref name="TValue"/> has no index binding.
    /// </exception>
    [RUF(Messages.Reflection), RDC(Messages.DynamicCode)]
    IDematerializer<T, TValue> GetDematerializer<[DAM(Messages.ReflectionBound)] TValue>();
}
