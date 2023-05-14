using System.Diagnostics.CodeAnalysis;

namespace FlameCsv.Binding.Attributes;

/// <inheritdoc/>
public sealed class CsvConverterAttribute<T, [DynamicallyAccessedMembers(Messages.Ctors)] TParser> : CsvConverterAttribute<T>
    where T : unmanaged, IEquatable<T>
    where TParser : CsvConverter<T>
{
    /// <inheritdoc/>
    public CsvConverterAttribute() : base(typeof(TParser))
    {
    }
}
