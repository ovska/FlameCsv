namespace FlameCsv.Binding.Attributes;

/// <summary>
/// Marks the field at indexes as ignored.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class CsvIndexIgnoreAttribute : Attribute, ICsvBindingAttribute
{
    /// <summary>Ignored field indexes.</summary>
    public ReadOnlySpan<int> Indexes => _indexes;

    /// <inheritdoc cref="ICsvBindingAttribute.Scope"/>
    public CsvBindingScope Scope { get; set; }

    private readonly int[] _indexes;

    /// <inheritdoc cref="CsvIndexIgnoreAttribute"/>
    /// <param name="indexes">CSV field indexes</param>
    public CsvIndexIgnoreAttribute(params int[] indexes)
    {
        ArgumentNullException.ThrowIfNull(indexes);

        for (int i = 0; i < indexes.Length; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(indexes[i]);
        }

        _indexes = indexes;
    }
}
